/****************************************************************************
 * @file:       dms_tdm.c
 * @project:    DMS - Time Division Multiplexed Jamming
 * @author:     K9 Electronics Ltd
 * @build:      2026-09-18a  (per-band attenuation applied per hop)
 * @brief:      Standalone TDM multi-band frequency hopping module.
 *
 *  This module adds autonomous multi-band jamming capability to the DMS
 *  board. Once configured via USB, the STM32 runs the TDM hop loop
 *  independently using direct SPI to MAX2871 (PLL) and AD9106 (DDS).
 *
 *  Architecture:
 *  - tdm_process() called every main loop iteration (~microseconds)
 *  - Uses DWT cycle counter for MICROSECOND dwell timing
 *  - PLL hop via MAX2871_SetFrequency() (~10us per SPI write)
 *  - Fast hop path: PLL-only when DDS mode/BW unchanged (~15us)
 *  - Full hop path: PLL + DDS reconfigure when mode/BW changes (~50us)
 *  - ENABLE pin (PC7) provides hardware start/stop trigger
 *  - Band table + params saveable to EEPROM for power-on recall
 *
 *  Performance (v4.0 - microsecond hopping):
 *  - Fast hop: ~15us (PLL SPI only, PRBS keeps running)
 *  - Full hop: ~50us (PLL + DDS re-trigger)
 *  - PLL settling: ~50us additional
 *  - Minimum dwell: 50us (limited by PLL lock time)
 *  - With 12 bands @ 100us dwell: cycle = 1.2ms, revisit = 833 Hz
 *
 *  CRITICAL FIX (v3.7):
 *  - tdm_hop_to_band() now performs full AD9106 register latch +
 *    DDS_CTRL manual trigger pulse after each PLL frequency change.
 *    Without this, only band 1 outputs DDS bandwidth; subsequent bands
 *    show only a CW carrier because the AD9106 RAMP engine stalls
 *    when the PLL changes underneath it.
 ****************************************************************************/

/****************************************************************************
 *                      INCLUDES
 ****************************************************************************/
#include "dms_tdm.h"
#include "dms_functions.h"
#include "syscom.h"
#include "crc16.h"

/* External driver functions - signatures verified against driver headers */
extern int    MAX2871_SetFrequency(double fFrequency, int waitLD);   /* max2871.h */
extern double MAX2871_GetFrequency(void);                             /* max2871.h */
extern int    MAX2871_GetLD(void);                                    /* max2871.h */
extern void   MAX2871_Register(uint32_t ulRegister);                  /* max2871.h — array only */
extern void   MAX2871_WriteRegister(uint32_t ulRegister);             /* max2871.h — SPI only */
extern unsigned long MAX2871_GetRegister(int index);                  /* max2871.h */

extern void           AD9106_Init(void);                              /* ad9106.h */
extern void           AD9106_SetFrequency(float freq);                /* ad9106.h */
extern void           AD9106_WriteRegister(unsigned short usRegister,  /* ad9106.h */
                                           unsigned short address);
extern void           AD9106_SetRegister(int address,                  /* ad9106.h */
                                         unsigned short reg);
extern unsigned short AD9106_GetRegister(unsigned short address);      /* ad9106.h */
extern void           AD9106_RampConfig_WriteToMemory(unsigned short endValue,  /* ad9106.h */
                                                      unsigned short numSamples);

extern int  AT24_I2C_MemoryRead(int addr, uint8_t* data, uint16_t len);   /* at24c32d.h */
extern int  AT24_I2C_MemoryWrite(int addr, uint8_t* data, uint16_t len);  /* at24c32d.h */

extern uint16_t CalculateCRC16(uint8_t* data, int len);              /* crc16.h */

/* External hardware handles */
extern TIM_HandleTypeDef htim4;
extern DAC_HandleTypeDef hdac;

/* External device config */
extern tsDeviceCfg DeviceCfg;

/* External pulse timer function - verified against pulse_timer.h */
extern void pulse_timer_ctrl(TIM_HandleTypeDef *htim, uint32_t TIM_Channel,
                             int pulse_ctrl, float frequency, float duration);

/* Pulse timer control values (from pulse_timer.h) */
#ifndef PULSE_TIMER_ON
#define PULSE_TIMER_ON  1
#endif
#ifndef PULSE_TIMER_OFF
#define PULSE_TIMER_OFF 0
#endif

/****************************************************************************
 *                      VARIABLES
 ****************************************************************************/
/** Global TDM configuration and state */
tsTdmConfig TdmCfg;

/** Previous ENABLE pin state for edge detection */
static uint8_t tdm_enable_prev = 0;

/** Fault code for LED indication */
static uint8_t tdm_fault = TDM_FAULT_NONE;

/** LED timing */
static uint32_t led_last_tick = 0;
static uint8_t  led_toggle = 0;

/** HOT-HOP: Keep CTRL timer running between RAMP hops.
 *  Once started in tdm_configure_dds_ramp, the timer runs continuously
 *  at the widest band's ctrl_freq.  Hops only change PLL + STOP_ADDR
 *  without stopping/restarting the timer → zero DDS dropout. */
static float    tdm_running_ctrl_freq = 0;   /* Current timer frequency (Hz) */
static uint8_t  tdm_timer_running = 0;        /* 1 = TIM4 CH3 is active */

/* ── TIMING DIAGNOSTICS ──
 * Measured via DWT cycle counter, reported via CMD_TDM_STATUS.
 * Updated every hop — rolling exponential average (α=0.125). */
static uint32_t tdm_diag_last_process_cyc = 0;  /* DWT at last tdm_process entry */
static uint32_t tdm_diag_loop_us   = 0;  /* µs between tdm_process() calls (main loop period) */
static uint32_t tdm_diag_hop_us    = 0;  /* µs inside tdm_hop_to_band() */
static uint32_t tdm_diag_dwell_us  = 0;  /* µs actual measured dwell (hop-to-hop) */
static uint32_t tdm_diag_cycle_us  = 0;  /* µs full cycle (all bands once) */
static uint32_t tdm_diag_cycle_start = 0; /* DWT at start of cycle */

/* ── PRE-COMPUTED PLL REGISTERS for fast hopping ──
 * MAX2871_SetFrequency uses double-precision pow(), floor(), division
 * → ~300µs on Cortex-M4 (no hardware double FPU).
 * Pre-compute Reg0 (N+Frac) and Reg4 (VcoDiv) per band at start time,
 * then during hops just SPI-write the cached values (~10µs). */
static uint32_t tdm_pll_reg0[TDM_MAX_BANDS];  /* Cached REG0 per band */
static uint32_t tdm_pll_reg1[TDM_MAX_BANDS];  /* Cached REG1 per band */
static uint32_t tdm_pll_reg3[TDM_MAX_BANDS];  /* Cached REG3 per band */
static uint32_t tdm_pll_reg4[TDM_MAX_BANDS];  /* Cached REG4 per band */
static uint32_t tdm_pll_reg5;                  /* REG5 — same for all bands */

/* -- BAND GROUP ROTATION --
 * Rotate between groups of N bands when total active bands exceed what
 * can be simultaneously jammed (e.g. 8 bands, 4 per group, 10ms rotation).
 * group_size=0 means disabled (all bands hop normally). */
static uint8_t  tdm_group_size     = 0;    /* Bands per group (0=disabled) */
static uint16_t tdm_rotation_ms    = 10;   /* Time per group in ms */
static uint8_t  tdm_group_offset   = 0;    /* Current start index in active_list */
static uint32_t tdm_group_tick     = 0;    /* HAL_GetTick at last rotation */
static uint8_t  tdm_group_count    = 0;    /* Number of groups (computed) */
static uint8_t  tdm_group_current  = 0;    /* Current group index */

/**
 * @brief   Pre-compute PLL register values for all active bands.
 *          Called once during tdm_start() — the expensive math happens here.
 *          During hopping, tdm_pll_fast_hop() just writes cached values.
 */
static void tdm_precompute_pll_regs(void)
{
    for (int i = 0; i < TdmCfg.active_count; i++)
    {
        uint8_t idx = TdmCfg.active_list[i];
        tsTdmBand* band = &TdmCfg.bands[idx];

        /* Call the slow SetFrequency to compute correct N/Frac/VcoDiv */
        MAX2871_SetFrequency(band->lo_frequency, 0);

        /* Cache ALL register values — write full 5→0 sequence during hop */
        tdm_pll_reg0[idx] = MAX2871_GetRegister(0);   /* REG0: N + Frac */
        tdm_pll_reg1[idx] = MAX2871_GetRegister(1);   /* REG1: Modulus */
        tdm_pll_reg3[idx] = MAX2871_GetRegister(3);   /* REG3: VCO + phase */
        tdm_pll_reg4[idx] = MAX2871_GetRegister(4);   /* REG4: VcoDiv + RFA_EN */
    }
    tdm_pll_reg5 = MAX2871_GetRegister(5);             /* REG5: same for all */
}

/**
 * @brief   Fast PLL hop using pre-computed registers.
 *          No floating point. No RFA_EN toggle. No busy-wait.
 *          Just 1-2 SPI writes (~10-20µs).
 *
 * @param   band_idx    Band index to hop to.
 */
static void tdm_pll_fast_hop(uint8_t band_idx)
{
    /* Write full register sequence 5→4→3→2→1→0 via SPI.
     * MAX2871 requires descending order — writing REG0 last triggers
     * VCO autocalibration with all other registers already set.
     * ~60µs per hop, clean output, no VCO feedthrough. */
    uint32_t reg2 = MAX2871_GetRegister(2);

    MAX2871_Register(tdm_pll_reg5);
    MAX2871_Register(tdm_pll_reg4[band_idx]);
    MAX2871_Register(tdm_pll_reg3[band_idx]);
    MAX2871_Register(reg2);
    MAX2871_Register(tdm_pll_reg1[band_idx]);
    MAX2871_Register(tdm_pll_reg0[band_idx]);

    MAX2871_WriteRegister(tdm_pll_reg5);
    MAX2871_WriteRegister(tdm_pll_reg4[band_idx]);
    MAX2871_WriteRegister(tdm_pll_reg3[band_idx]);
    MAX2871_WriteRegister(reg2);
    MAX2871_WriteRegister(tdm_pll_reg1[band_idx]);
    MAX2871_WriteRegister(tdm_pll_reg0[band_idx]);
}

/****************************************************************************
 *                  STATIC FUNCTION DECLARATIONS
 ****************************************************************************/
static void tdm_configure_dds_ramp(tsTdmBand* band);
static void tdm_configure_dds_prbs(tsTdmBand* band);
static void tdm_fill_sram_random(uint16_t maxValue, uint16_t count);
static void tdm_switch_dds_mode(uint8_t new_mode, tsTdmBand* band);
static void tdm_reconfigure_dds_bandwidth(tsTdmBand* band);
static uint16_t tdm_compute_twmem(float bwHz);
static uint16_t tdm_compute_stop_addr(float bwHz);
static void tdm_hop_to_band(uint8_t band_idx);
static void tdm_build_active_list(void);
static void tdm_save_hardware_state(void);
static void tdm_restore_hardware_state(void);
static void tdm_dds_ctrl_config(int flag);
static void tdm_dds_trigger_sequence(void);
static void tdm_dds_latch_and_trigger(float ctrl_freq);
static void tdm_dds_retrigger(float ctrl_freq);
static void tdm_kill_dds_output(void);
static void tdm_set_led(uint8_t colour);

/****************************************************************************
 *                  PUBLIC FUNCTIONS
 ****************************************************************************/

/**
 * @brief   Initialize TDM module with safe defaults.
 */
void tdm_init(void)
{
    memset(&TdmCfg, 0, sizeof(tsTdmConfig));

    TdmCfg.mode        = TDM_MODE_OFF;
    TdmCfg.num_bands   = 0;
    TdmCfg.dwell_us    = 2000;     /* 2ms default (was 5ms) */
    TdmCfg.pulse_on_ms = 120;
    TdmCfg.pulse_off_ms = 30;
    TdmCfg.duty_percent = 100;     /* 100% = full SRAM ramp (no pulsing) */
    TdmCfg.state       = TDM_STATE_IDLE;

    /* Reset hot-hop timer state */
    tdm_running_ctrl_freq = 0;
    tdm_timer_running = 0;

    /* Initialize ARM DWT cycle counter for microsecond timing */
    DWT_INIT();

    /* Try to load saved config from EEPROM */
    if (tdm_load_from_eeprom() < 0)
    {
        tdm_fault = TDM_FAULT_EEPROM;
    }
    else if (TdmCfg.mode != TDM_MODE_OFF && TdmCfg.num_bands > 0)
    {
        /* Valid config found in EEPROM - auto-start standalone jamming.
         * Delay allows PLL, DDS and PA supplies to stabilise after boot. */
        HAL_Delay(500);
        tdm_start();
    }
}

/**
 * @brief   Main TDM process function. Called every main loop iteration.
 *          Zero overhead when idle. Handles hopping when active.
 *
 *  v4.0: Uses DWT cycle counter for MICROSECOND dwell timing.
 *  Pulse mode still uses HAL_GetTick (ms resolution is fine for pulse ON/OFF).
 */
void tdm_process(void)
{
    uint32_t now_ms;
    uint32_t elapsed_ms;
    uint32_t now_cyc;
    uint32_t elapsed_us;

    /* Fast exit if not running */
    if (TdmCfg.state == TDM_STATE_IDLE)
        return;

    /* ── PULSED MODE: handle ON/OFF phases (ms resolution OK) ── */
    if (TdmCfg.mode == TDM_MODE_PULSED)
    {
        now_ms = HAL_GetTick();
        elapsed_ms = now_ms - TdmCfg.pulse_tick;

        if (TdmCfg.state == TDM_STATE_RUNNING)
        {
            /* Currently in ON phase - check if pulse_on_ms elapsed */
            if (elapsed_ms >= TdmCfg.pulse_on_ms)
            {
                /* Transition to SILENT phase */
                tdm_kill_dds_output();
                TdmCfg.state = TDM_STATE_SILENT;
                TdmCfg.pulse_tick = now_ms;
                return;
            }
        }
        else if (TdmCfg.state == TDM_STATE_SILENT)
        {
            /* Currently in OFF phase - check if pulse_off_ms elapsed */
            if (elapsed_ms >= TdmCfg.pulse_off_ms)
            {
                /* Transition back to ON phase - restart DDS output */
                tdm_dds_trigger_sequence();
                TdmCfg.state = TDM_STATE_RUNNING;
                TdmCfg.pulse_tick = now_ms;
            }
            return;  /* Don't hop during silent phase */
        }
    }

    /* ── FREQUENCY HOPPING (microsecond timing via DWT) ── */
    now_cyc = DWT_CYCLES();
    elapsed_us = DWT_US(now_cyc - TdmCfg.last_hop_cyc);

    /* ── DIAGNOSTIC: measure main loop period ── */
    if (tdm_diag_last_process_cyc != 0)
    {
        uint32_t loop = DWT_US(now_cyc - tdm_diag_last_process_cyc);
        /* Exponential moving average: new = old + (sample - old)/8 */
        tdm_diag_loop_us = tdm_diag_loop_us + (loop - tdm_diag_loop_us) / 8;
    }
    tdm_diag_last_process_cyc = now_cyc;

    /* -- GROUP ROTATION CHECK (ms resolution) -- */
    if (tdm_group_size > 0 && tdm_group_size < TdmCfg.active_count)
    {
        uint32_t rot_elapsed = HAL_GetTick() - tdm_group_tick;
        if (rot_elapsed >= tdm_rotation_ms)
        {
            tdm_group_current++;
            if (tdm_group_current >= tdm_group_count)
                tdm_group_current = 0;
            tdm_group_offset = tdm_group_current * tdm_group_size;
            TdmCfg.active_idx = tdm_group_offset;
            tdm_group_tick = HAL_GetTick();
        }
    }

    if (elapsed_us >= TdmCfg.dwell_us)
    {
        /* Advance to next active band (within group if rotation active) */
        TdmCfg.active_idx++;

        if (tdm_group_size > 0 && tdm_group_size < TdmCfg.active_count)
        {
            uint8_t group_end = tdm_group_offset + tdm_group_size;
            if (group_end > TdmCfg.active_count)
                group_end = TdmCfg.active_count;
            if (TdmCfg.active_idx >= group_end)
                TdmCfg.active_idx = tdm_group_offset;
        }
        else
        {
            if (TdmCfg.active_idx >= TdmCfg.active_count)
                TdmCfg.active_idx = 0;
        }

        uint8_t next_band = TdmCfg.active_list[TdmCfg.active_idx];

        /* ── ALL BANDS: hop with DDS retrigger ──
         * tdm_hop_to_band() does PLL change + latch + trigger + timer restart.
         * For single band, skip PLL (same freq) — retrigger DDS only. */
        if (TdmCfg.active_count == 1)
        {
            /* ── SINGLE BAND: refresh DDS_CYC every hop ── */
            for (int i = 0; i < 4; i++)
                AD9106_WriteRegister(0xFFFF, (uint16_t)(0x53 + 4*i));
            AD9106_WriteRegister(0x0001, 0x1D);
        }
        else
        {
            /* ── MULTI BAND: full hop with PLL change ── */
            uint32_t hop_start = DWT_CYCLES();
            tdm_hop_to_band(next_band);
            uint32_t hop_dur = DWT_US(DWT_CYCLES() - hop_start);

            /* DIAGNOSTIC: hop duration (exponential avg) */
            tdm_diag_hop_us = tdm_diag_hop_us + (hop_dur - tdm_diag_hop_us) / 8;

            /* DIAGNOSTIC: actual measured dwell (time since last hop) */
            tdm_diag_dwell_us = tdm_diag_dwell_us + (elapsed_us - tdm_diag_dwell_us) / 8;

            /* DIAGNOSTIC: full cycle time (all bands once) */
            if (TdmCfg.active_idx == 0)
            {
                if (tdm_diag_cycle_start != 0)
                {
                    uint32_t cyc = DWT_US(DWT_CYCLES() - tdm_diag_cycle_start);
                    tdm_diag_cycle_us = tdm_diag_cycle_us + (cyc - tdm_diag_cycle_us) / 8;
                }
                tdm_diag_cycle_start = DWT_CYCLES();
            }

            /* Check PLL lock periodically (every 1024 hops — ~1s) */
            if ((TdmCfg.hop_count & 0x3FF) == 0)
            {
                if (MAX2871_GetLD() == 0)
                    tdm_fault = TDM_FAULT_PLL;
                else if (tdm_fault == TDM_FAULT_PLL)
                    tdm_fault = TDM_FAULT_NONE;
            }
        }

        TdmCfg.last_hop_cyc = DWT_CYCLES();
        TdmCfg.hop_count++;
    }
}

/**
 * @brief   Start TDM hopping.
 */
int tdm_start(void)
{
    if (TdmCfg.state != TDM_STATE_IDLE)
        return -2;  /* Already running */

    /* Build list of active bands */
    tdm_build_active_list();

    if (TdmCfg.active_count == 0)
    {
        tdm_fault = TDM_FAULT_NO_BANDS;
        return -1;  /* No active bands */
    }

    /* Clear any previous faults on successful start */
    tdm_fault = TDM_FAULT_NONE;

    /* Save current hardware state for restore on stop */
    tdm_save_hardware_state();

    /* ── DDS mode initialization ──
     * Boot process calls AD9106_Init() which sets default RAMP state.
     * For RAMP: keep boot state, just set tracking vars.
     * For PRBS: full PSEUDO register configuration needed.
     * tdm_hop_to_band() handles per-band mode switching after this.
     *
     * SRAM strategy: SRAM is written ONCE here and reused for all hops.
     * If ANY non-PRBS band uses RANDOM mode, fill SRAM with shuffled
     * data for all bands. This is safe because Random SRAM covers the
     * same frequencies as linear ramp, just in unpredictable order —
     * equal or better jamming effectiveness on all targets. */
    uint8_t first_band_idx = TdmCfg.active_list[0];
    uint8_t first_mode = TdmCfg.bands[first_band_idx].dds_mode;

    /* Scan for any RANDOM band — if found, all RAMP bands also use shuffled SRAM */
    uint8_t any_random = 0;
    for (uint8_t i = 0; i < TdmCfg.active_count; i++)
    {
        if (TdmCfg.bands[TdmCfg.active_list[i]].dds_mode == TDM_DDS_MODE_RANDOM)
        {
            any_random = 1;
            break;
        }
    }

    if (first_mode == TDM_DDS_MODE_PRBS)
    {
        /* First band is PRBS — configure PSEUDO mode.
         * If we also have RAMP/RANDOM bands, pre-fill SRAM now so that
         * switching PRBS→RAMP/RANDOM during hopping finds valid data. */
        tdm_configure_dds_prbs(&TdmCfg.bands[first_band_idx]);
        TdmCfg.current_dds_mode = TDM_DDS_MODE_PRBS;
        DeviceCfg.DDS->sweep_ctrl = 3;  /* AD9106_PSEUDO */

        /* Pre-fill SRAM for any non-PRBS bands that will need it later.
         * Always fill ALL 4096 entries with full 12-bit ramp (0→4095).
         * BW is controlled per-band by STOP_ADDR + TwMem via C# formula. */
        float widest_bw = 0;
        for (uint8_t i = 0; i < TdmCfg.active_count; i++)
        {
            tsTdmBand* b = &TdmCfg.bands[TdmCfg.active_list[i]];
            if (b->dds_mode != TDM_DDS_MODE_PRBS)
            {
                if (b->dds_bandwidth > widest_bw)
                    widest_bw = b->dds_bandwidth;
            }
        }
        if (widest_bw > 0)
        {
            uint16_t maxRV;
            if (any_random)
            {
                uint32_t rv = (uint32_t)((widest_bw / 170000000.0f) * 4095.0f);
                if (rv < 32) rv = 32;
                if (rv > 4095) rv = 4095;
                maxRV = (uint16_t)rv;
            }
            else
            {
                maxRV = 4095;
            }

            TdmCfg.sram_bw      = widest_bw;
            TdmCfg.sram_twMem   = 0;
            TdmCfg.sram_maxRV   = maxRV;
            TdmCfg.sram_entries  = 4096;  /* Always fill entire SRAM */

            /* Write ramp to SRAM (PRBS ignores SRAM content) */
            AD9106_WriteRegister(0x0004, 0x1E);  /* Enable MEM_ACCESS */
            AD9106_WriteRegister(0x0001, 0x1D);
            if (any_random)
                tdm_fill_sram_random(maxRV, 4096);
            else
                AD9106_RampConfig_WriteToMemory(maxRV, 4096);
            AD9106_WriteRegister(0x0000, 0x1E);  /* Disable MEM_ACCESS */
            AD9106_WriteRegister(0x0001, 0x1D);
        }
    }
    else
    {
        /* RAMP or RANDOM mode — same registers, different SRAM content.
         * tdm_configure_dds_ramp() internally scans all active bands for
         * RANDOM mode and uses shuffled SRAM if ANY band is RANDOM.
         * This ensures SRAM stays valid when hopping between RAMP/RANDOM bands
         * (SRAM refill during hopping would take ~1ms, too slow for 100µs dwell). */
        tdm_configure_dds_ramp(&TdmCfg.bands[first_band_idx]);
        TdmCfg.current_dds_mode = first_mode;  /* RAMP(0) or RANDOM(2) */
        DeviceCfg.DDS->sweep_ctrl = 2;  /* AD9106_RAMP (registers identical) */
    }

    /* Force bandwidth reconfigure on first hop */
    TdmCfg.current_bw = 0.0f;

    /* K9 FIX: Apply the SAVED attenuator setting (position 0), loaded from
     * EEPROM on boot / set live via the software attenuator box + SAVE TO DEVICE.
     * Previously this was forced to DAC=0 (full power), which silently overrode
     * whatever attenuation the user set and saved. */
    HAL_DAC_Start(&hdac, DAC1_CHANNEL_1);
    HAL_DAC_SetValue(&hdac, DAC1_CHANNEL_1, DAC_ALIGN_12B_R, DeviceCfg.Attenuator.Dac[0]);

    /* Set initial band */
    TdmCfg.active_idx = 0;

    /* Pre-compute PLL registers AFTER DDS is configured and running.
     * SetFrequency cycles through all bands (briefly changes PLL)
     * but DDS timer keeps running independently on AD9106. */
    tdm_precompute_pll_regs();

    /* Hop to first band — DDS was configured for this band, PLL needs
     * to be set back to band 0 after precompute cycled through all bands. */
    tdm_hop_to_band(TdmCfg.active_list[0]);

    /* Initialize timing */
    TdmCfg.last_hop_tick = HAL_GetTick();
    TdmCfg.last_hop_cyc  = DWT_CYCLES();
    TdmCfg.pulse_tick    = HAL_GetTick();
    TdmCfg.hop_count     = 0;

    /* Reset timing diagnostics */
    tdm_diag_last_process_cyc = 0;
    tdm_diag_loop_us  = 0;
    tdm_diag_hop_us   = 0;
    tdm_diag_dwell_us = 0;
    tdm_diag_cycle_us = 0;
    tdm_diag_cycle_start = 0;

    /* Initialize group rotation */
    if (tdm_group_size > 0 && tdm_group_size < TdmCfg.active_count)
    {
        tdm_group_count = (TdmCfg.active_count + tdm_group_size - 1) / tdm_group_size;
        tdm_group_current = 0;
        tdm_group_offset = 0;
        tdm_group_tick = HAL_GetTick();
    }
    else
    {
        tdm_group_count = 1;
        tdm_group_current = 0;
        tdm_group_offset = 0;
    }

    /* Set running state */
    TdmCfg.state = TDM_STATE_RUNNING;

    /* Fast LED blink to indicate TDM active */
    DeviceCfg.Led.Interval = 10;

    return 0;
}

/**
 * @brief   Stop TDM hopping and restore previous state.
 */
void tdm_stop(void)
{
    if (TdmCfg.state == TDM_STATE_IDLE)
        return;

    /* Kill DDS output immediately */
    tdm_kill_dds_output();

    /* Stop DDS control timer */
    tdm_dds_ctrl_config(0);
    pulse_timer_ctrl(&htim4, TIM_CHANNEL_3, PULSE_TIMER_OFF, 0, 0);
    tdm_timer_running = 0;
    tdm_running_ctrl_freq = 0;

    /* Restore previous hardware state */
    tdm_restore_hardware_state();

    /* Back to idle */
    TdmCfg.state = TDM_STATE_IDLE;
    TdmCfg.active_idx = 0;
    TdmCfg.hop_count = 0;

    /* Restore normal LED blink rate */
    DeviceCfg.Led.Interval = 100;
}

/**
 * @brief   Configure a band in the TDM table.
 */
int tdm_set_band(uint8_t index, tsTdmBand* band)
{
    if (index >= TDM_MAX_BANDS)
        return -1;

    memcpy(&TdmCfg.bands[index], band, sizeof(tsTdmBand));

    if (index >= TdmCfg.num_bands)
        TdmCfg.num_bands = index + 1;

    /* Rebuild active list whenever a band changes */
    tdm_build_active_list();

    return 0;
}

/**
 * @brief   Set TDM operating parameters.
 */
void tdm_set_params(uint8_t mode, uint16_t dwell_us,
                    uint16_t pulse_on_ms, uint16_t pulse_off_ms,
                    uint8_t duty_percent)
{
    TdmCfg.mode         = mode;
    TdmCfg.dwell_us     = (dwell_us < TDM_MIN_DWELL_US) ? TDM_MIN_DWELL_US : dwell_us;
    TdmCfg.pulse_on_ms  = pulse_on_ms;
    TdmCfg.pulse_off_ms = pulse_off_ms;
    /* Clamp duty 5-100%, treat 0 as 100% for backward compat */
    if (duty_percent == 0) duty_percent = 100;
    if (duty_percent < 5)  duty_percent = 5;
    TdmCfg.duty_percent = duty_percent;
    /* Pre-compute active SRAM entries */
    TdmCfg.active_sram = (uint16_t)((4096UL * (uint32_t)duty_percent) / 100UL);
    if (TdmCfg.active_sram < 32) TdmCfg.active_sram = 32;
}

/**
 * @brief   Check ENABLE pin for hardware start/stop trigger.
 *          Rising edge = start, falling edge = stop.
 */
void tdm_check_enable_pin(void)
{
    uint8_t pin_state;

    /* Only respond to ENABLE pin if TDM bands are configured */
    if (TdmCfg.num_bands == 0 || TdmCfg.mode == TDM_MODE_OFF)
        return;

    pin_state = (HAL_GPIO_ReadPin(ENABLE_GPIO_Port, ENABLE_Pin) == GPIO_PIN_SET) ? 1 : 0;

    /* Rising edge: start TDM */
    if (pin_state == 1 && tdm_enable_prev == 0)
    {
        if (TdmCfg.state == TDM_STATE_IDLE)
            tdm_start();
    }
    /* Falling edge: stop TDM */
    else if (pin_state == 0 && tdm_enable_prev == 1)
    {
        if (TdmCfg.state != TDM_STATE_IDLE)
            tdm_stop();
    }

    tdm_enable_prev = pin_state;
}

/**
 * @brief   Process TDM USB commands (types 40-48).
 * @retval  1 if command was handled, 0 if not a TDM command
 */
int tdm_process_command(void* rxCmd, void* txCmd)
{
    tsSysCmd* pRx = (tsSysCmd*)rxCmd;
    tsSysCmd* pTx = (tsSysCmd*)txCmd;

    switch (pRx->Type)
    {
        case CMD_SET_TDM_BAND:
        {
            /*  data[0]      = band index (0-7)
             *  data[1..8]   = lo_frequency (double, 8 bytes)
             *  data[9..12]  = dds_bandwidth (float)
             *  data[13..16] = dds_step (float)
             *  data[17..20] = dds_points (uint32)
             *  data[21..24] = dds_ctrl_freq (float)
             *  data[25]     = active (0/1)
             *  data[26]     = dds_mode (0=RAMP, 1=PRBS) — optional for backward compat
             */
            if (pRx->Len >= 26)
            {
                tsTdmBand band;
                uint8_t idx = pRx->data[0];
                band.lo_frequency   = *((double*)&pRx->data[1]);
                band.dds_bandwidth  = *((float*)&pRx->data[9]);
                band.dds_step       = *((float*)&pRx->data[13]);
                band.dds_points     = *((uint32_t*)&pRx->data[17]);
                band.dds_ctrl_freq  = *((float*)&pRx->data[21]);
                band.active         = pRx->data[25];
                band.dds_mode       = (pRx->Len >= 27) ? pRx->data[26] : 0;  /* Default RAMP */
                band.atten_dac      = 0xFFFF;  /* live single-band set: use main attenuator */

                if (tdm_set_band(idx, &band) != 0)
                    pTx->Status = CMD_RECV_CMD_ERROR;
            }
            else
            {
                pTx->Status = CMD_RECV_CMD_ERROR;
            }
            return 1;
        }

        case CMD_GET_TDM_BAND:
        {
            /* data[0] = band index to read */
            if (pRx->Len >= 1 && pRx->data[0] < TDM_MAX_BANDS)
            {
                uint8_t idx = pRx->data[0];
                pTx->Len = 27;
                pTx->data[0] = idx;
                *((double*)&pTx->data[1])   = TdmCfg.bands[idx].lo_frequency;
                *((float*)&pTx->data[9])    = TdmCfg.bands[idx].dds_bandwidth;
                *((float*)&pTx->data[13])   = TdmCfg.bands[idx].dds_step;
                *((uint32_t*)&pTx->data[17])= TdmCfg.bands[idx].dds_points;
                *((float*)&pTx->data[21])   = TdmCfg.bands[idx].dds_ctrl_freq;
                pTx->data[25]               = TdmCfg.bands[idx].active;
                pTx->data[26]               = TdmCfg.bands[idx].dds_mode;
            }
            else
            {
                pTx->Status = CMD_RECV_CMD_ERROR;
            }
            return 1;
        }

        case CMD_SET_TDM_PARAMS:
        {
            /* data[0]   = mode (0/1/2)
             * data[1..2] = dwell_us (MICROSECONDS)
             * data[3..4] = pulse_on_ms
             * data[5..6] = pulse_off_ms
             * data[7]   = duty_percent (5-100, 0=100% backward compat)
             */
            if (pRx->Len >= 7)
            {
                uint8_t duty = (pRx->Len >= 8) ? pRx->data[7] : 100;
                tdm_set_params(
                    pRx->data[0],
                    *((uint16_t*)&pRx->data[1]),
                    *((uint16_t*)&pRx->data[3]),
                    *((uint16_t*)&pRx->data[5]),
                    duty
                );
            }
            else
            {
                pTx->Status = CMD_RECV_CMD_ERROR;
            }
            return 1;
        }

        case CMD_GET_TDM_PARAMS:
        {
            pTx->Len = 8;
            pTx->data[0] = TdmCfg.mode;
            *((uint16_t*)&pTx->data[1]) = TdmCfg.dwell_us;
            *((uint16_t*)&pTx->data[3]) = TdmCfg.pulse_on_ms;
            *((uint16_t*)&pTx->data[5]) = TdmCfg.pulse_off_ms;
            pTx->data[7] = TdmCfg.duty_percent;
            return 1;
        }

        case CMD_TDM_START:
        {
            int result = tdm_start();
            if (result != 0)
                pTx->Status = CMD_DEVICE_ERROR;
            return 1;
        }

        case CMD_TDM_STOP:
        {
            tdm_stop();
            return 1;
        }

        case 49:  /* CMD_SET_GROUP_ROTATION */
        {
            /* data[0]=group_size (0=off, 4=typical), data[1..2]=rotation_ms */
            if (pRx->Len >= 3)
            {
                tdm_group_size = pRx->data[0];
                tdm_rotation_ms = *((uint16_t*)&pRx->data[1]);
                if (tdm_rotation_ms < 1) tdm_rotation_ms = 1;
                if (TdmCfg.state != TDM_STATE_IDLE && tdm_group_size > 0 &&
                    tdm_group_size < TdmCfg.active_count)
                {
                    tdm_group_count = (TdmCfg.active_count + tdm_group_size - 1) / tdm_group_size;
                    tdm_group_current = 0;
                    tdm_group_offset = 0;
                    TdmCfg.active_idx = 0;
                    tdm_group_tick = HAL_GetTick();
                }
                else if (tdm_group_size == 0)
                {
                    tdm_group_count = 1;
                    tdm_group_current = 0;
                    tdm_group_offset = 0;
                }
            }
            return 1;
        }

        case CMD_TDM_STATUS:
        {
            pTx->Len = 24;  /* Must fit within data[26] (SYSCOM_SIZE=32) */
            pTx->data[0] = TdmCfg.state;
            pTx->data[1] = TdmCfg.active_count;
            pTx->data[2] = TdmCfg.current_band;
            pTx->data[3] = TdmCfg.num_bands;
            *((uint32_t*)&pTx->data[4]) = TdmCfg.hop_count;
            *((uint16_t*)&pTx->data[8]) = TdmCfg.dwell_us;
            /* Cycles per second: 1,000,000 / (dwell_us * active_count) */
            {
                uint32_t cycle_us = (uint32_t)TdmCfg.dwell_us * TdmCfg.active_count;
                uint16_t cps = (cycle_us > 0) ? (uint16_t)(1000000UL / cycle_us) : 0;
                if (cps > 65535) cps = 65535;
                *((uint16_t*)&pTx->data[10]) = cps;
            }
            /* ── TIMING DIAGNOSTICS (bytes 12-19, uint16 — max 65535µs) ── */
            *((uint16_t*)&pTx->data[12]) = (tdm_diag_loop_us > 65535) ? 65535 : (uint16_t)tdm_diag_loop_us;
            *((uint16_t*)&pTx->data[14]) = (tdm_diag_hop_us > 65535) ? 65535 : (uint16_t)tdm_diag_hop_us;
            *((uint16_t*)&pTx->data[16]) = (tdm_diag_dwell_us > 65535) ? 65535 : (uint16_t)tdm_diag_dwell_us;
            *((uint16_t*)&pTx->data[18]) = (tdm_diag_cycle_us > 65535) ? 65535 : (uint16_t)tdm_diag_cycle_us;
            /* Remaining fields */
            *((uint16_t*)&pTx->data[20]) = TdmCfg.active_sram;
            pTx->data[22] = tdm_group_size;
            pTx->data[23] = tdm_group_current;
            return 1;
        }

        case CMD_TDM_SAVE_EEPROM:
        {
            if (tdm_save_to_eeprom() != 0)
                pTx->Status = CMD_WRITE_EEPROM_ERROR;
            return 1;
        }

        case CMD_TDM_LOAD_EEPROM:
        {
            /* Safety: stop TDM if currently running before loading new config */
            if (TdmCfg.state != TDM_STATE_IDLE)
                tdm_stop();
            int result = tdm_load_from_eeprom();
            if (result != 0)
                pTx->Status = CMD_READ_EEPROM_ERROR;
            return 1;
        }

        default:
            return 0;  /* Not a TDM command */
    }
}

/**
 * @brief   Save TDM config to EEPROM.
 */
int tdm_save_to_eeprom(void)
{
    uint8_t data[32];
    uint16_t crc;
    int result;

    /* ── Save header (16 bytes + 2 CRC) ── */
    memset(data, 0, 32);
    data[0] = TdmCfg.mode;
    data[1] = TdmCfg.num_bands;
    *((uint16_t*)&data[2]) = TdmCfg.dwell_us;
    *((uint16_t*)&data[4]) = TdmCfg.pulse_on_ms;
    *((uint16_t*)&data[6]) = TdmCfg.pulse_off_ms;
    /* Signature byte to detect valid TDM config */
    data[8] = 0x4B;  /* 'K' for K9 */
    data[9] = 0x39;  /* '9' */
    data[10] = 0x03; /* Version 3 = microsecond dwell + duty cycle */
    data[11] = TdmCfg.duty_percent;  /* SRAM duty 5-100% */
    crc = CalculateCRC16(data, 16);
    *((uint16_t*)&data[16]) = crc;

    result = AT24_I2C_MemoryWrite(TDM_EEPROM_HEADER, data, 18);
    if (result < 0) return -1;
    HAL_Delay(10);  /* EEPROM write time */

    /* ── Save bands (28 bytes each + 2 CRC = 30, fits in 32-byte page) ── */
    for (int i = 0; i < TDM_MAX_BANDS; i++)
    {
        memset(data, 0, 32);
        memcpy(data, &TdmCfg.bands[i], sizeof(tsTdmBand));
        crc = CalculateCRC16(data, sizeof(tsTdmBand));
        *((uint16_t*)&data[sizeof(tsTdmBand)]) = crc;

        result = AT24_I2C_MemoryWrite(
            TDM_EEPROM_BANDS + (i * 32), data, sizeof(tsTdmBand) + 2);
        if (result < 0) return -1;
        HAL_Delay(10);
    }

    return 0;
}

/**
 * @brief   Load TDM config from EEPROM.
 */
int tdm_load_from_eeprom(void)
{
    uint8_t data[32];
    uint16_t crc;
    int result;

    /* ── Read header ── */
    result = AT24_I2C_MemoryRead(TDM_EEPROM_HEADER, data, 18);
    if (result < 0) return -1;

    crc = CalculateCRC16(data, 16);
    if (crc != *((uint16_t*)&data[16])) return -2;

    /* Check signature */
    if (data[8] != 0x4B || data[9] != 0x39) return -2;

    TdmCfg.mode         = data[0];
    TdmCfg.num_bands    = data[1];
    TdmCfg.pulse_on_ms  = *((uint16_t*)&data[4]);
    TdmCfg.pulse_off_ms = *((uint16_t*)&data[6]);

    /* Backward compatibility: version byte at data[10]
     * Version 0x03 = microsecond dwell + duty cycle (newest)
     * Version 0x02 = microsecond dwell (no duty field)
     * Missing/0x00 = millisecond dwell (old format, convert) */
    if (data[10] >= 0x02)
    {
        TdmCfg.dwell_us = *((uint16_t*)&data[2]);
    }
    else
    {
        /* Old EEPROM: value is milliseconds, convert to microseconds */
        uint16_t old_ms = *((uint16_t*)&data[2]);
        TdmCfg.dwell_us = (old_ms < 1) ? 2000 : (old_ms * 1000);
    }

    /* Duty cycle: version 3+ stores at data[11], older = 100% default */
    if (data[10] >= 0x03 && data[11] >= 5 && data[11] <= 100)
    {
        TdmCfg.duty_percent = data[11];
    }
    else
    {
        TdmCfg.duty_percent = 100;  /* Backward compat: full duty */
    }

    /* TW_RAM + FTW: version 4+ stores C# library values at data[12-15].
     * These are the PROVEN values from setRampBandwidth() — firmware uses
     * them directly instead of trying to replicate the computation. */
    if (data[10] >= 0x04)
    {
        TdmCfg.eeprom_tw_ram = data[12];
        TdmCfg.eeprom_ftw    = (uint32_t)data[13]
                              | ((uint32_t)data[14] << 8)
                              | ((uint32_t)data[15] << 16);
    }
    else
    {
        TdmCfg.eeprom_tw_ram = 0xFF;  /* 0xFF = auto-compute (old EEPROM or no user override) */
        TdmCfg.eeprom_ftw    = 0;  /* 0 = firmware must compute (old EEPROM) */
    }
    /* Pre-compute active SRAM entries */
    TdmCfg.active_sram = (uint16_t)((4096UL * (uint32_t)TdmCfg.duty_percent) / 100UL);
    if (TdmCfg.active_sram < 32) TdmCfg.active_sram = 32;

    /* Validate */
    if (TdmCfg.num_bands > TDM_MAX_BANDS) TdmCfg.num_bands = TDM_MAX_BANDS;
    if (TdmCfg.dwell_us < TDM_MIN_DWELL_US) TdmCfg.dwell_us = 2000;

    /* ── Read bands ── */
    for (int i = 0; i < TDM_MAX_BANDS; i++)
    {
        result = AT24_I2C_MemoryRead(TDM_EEPROM_BANDS + (i * 32),
                                     data, sizeof(tsTdmBand) + 2);
        if (result < 0) continue;

        crc = CalculateCRC16(data, sizeof(tsTdmBand));
        if (crc == *((uint16_t*)&data[sizeof(tsTdmBand)]))
        {
            memcpy(&TdmCfg.bands[i], data, sizeof(tsTdmBand));
        }
    }

    /* Build active list from loaded bands */
    tdm_build_active_list();

    return 0;
}

/****************************************************************************
 *                  STATIC (PRIVATE) FUNCTIONS
 ****************************************************************************/

/**
 * @brief   Build ordered list of active band indices.
 */
static void tdm_build_active_list(void)
{
    TdmCfg.active_count = 0;
    for (int i = 0; i < TdmCfg.num_bands; i++)
    {
        if (TdmCfg.bands[i].active)
        {
            TdmCfg.active_list[TdmCfg.active_count] = i;
            TdmCfg.active_count++;
        }
    }
}

/**
 * @brief   Save current hardware state before TDM takes over.
 */
static void tdm_save_hardware_state(void)
{
    TdmCfg.saved_lo_freq    = MAX2871_GetFrequency();
    TdmCfg.saved_dds_mode   = DeviceCfg.DDS->sweep_ctrl;
    TdmCfg.saved_dds_begin  = DeviceCfg.DDS->begin;
    TdmCfg.saved_dds_step   = DeviceCfg.DDS->step;
    TdmCfg.saved_dds_points = DeviceCfg.DDS->points;
    TdmCfg.saved_dds_ctrl   = DeviceCfg.DDS->ctrl_freq;
}

/**
 * @brief   Restore hardware state after TDM stops.
 */
static void tdm_restore_hardware_state(void)
{
    /* Restore DDS mode and parameters */
    DeviceCfg.DDS->sweep_ctrl = TdmCfg.saved_dds_mode;
    DeviceCfg.DDS->begin      = TdmCfg.saved_dds_begin;
    DeviceCfg.DDS->step       = TdmCfg.saved_dds_step;
    DeviceCfg.DDS->points     = TdmCfg.saved_dds_points;
    DeviceCfg.DDS->ctrl_freq  = TdmCfg.saved_dds_ctrl;

    /* Reinitialize DDS with saved config */
    AD9106_Init();

    /* Restore DDS control timer */
    tdm_dds_ctrl_config((DeviceCfg.DDS->ctrl_freq > 0) ? 1 : 0);
    pulse_timer_ctrl(&htim4, TIM_CHANNEL_3,
                     (DeviceCfg.DDS->ctrl_freq > 0) ? PULSE_TIMER_ON : PULSE_TIMER_OFF,
                     DeviceCfg.DDS->ctrl_freq, 0.50);
    if (DeviceCfg.DDS->ctrl_freq > 0)
        AD9106_SetFrequency(DeviceCfg.DDS->begin);

    /* Restore PLL frequency */
    MAX2871_SetFrequency(TdmCfg.saved_lo_freq, DeviceCfg.WaitLD);
}

/**
 * @brief   Fill AD9106 SRAM with shuffled ramp values (RANDOM mode).
 *
 *  Creates the same set of frequency values as a linear ramp, but
 *  writes them to SRAM in random order using Fisher-Yates shuffle.
 *  Each CTRL tick now jumps to an unpredictable frequency instead
 *  of sweeping linearly.
 *
 *  This defeats adaptive anti-jam receivers (CRPA, Novatel GAJT,
 *  military GPS) that track and notch a predictable linear sweep.
 *
 *  Uses a 16-bit LFSR (maximal period 65535) as RNG — no stdlib
 *  dependency, deterministic but unpredictable to the target.
 *  LFSR polynomial: x^16 + x^14 + x^13 + x^11 + 1 (taps 16,14,13,11)
 *
 * @param   maxValue: Maximum DAC value (4095 for full scale)
 * @param   count: Number of active entries to fill (from duty %)
 */
static uint16_t sram_shuffle_buf[4096];  /* Static 8KB — avoids stack overflow */

static void tdm_fill_sram_random(uint16_t maxValue, uint16_t count)
{
    if (count < 2) count = 2;
    if (count > 4096) count = 4096;

    /* Step 1: Build linear ramp in buffer */
    float step = (float)maxValue / ((float)count - 1.0f);
    for (uint16_t i = 0; i < count; i++)
    {
        sram_shuffle_buf[i] = (uint16_t)(step * (float)i) & 0x0FFF;
    }

    /* Step 2: Fisher-Yates shuffle using 16-bit LFSR as RNG.
     * Seed from DWT cycle counter for different pattern each boot. */
    uint16_t lfsr = (uint16_t)(DWT_CYCLES() | 0x0001);  /* Must be non-zero */
    if (lfsr == 0) lfsr = 0xBEEF;

    for (uint16_t i = count - 1; i > 0; i--)
    {
        /* Advance LFSR (taps 16,14,13,11 → maximal period 65535) */
        uint16_t bit = ((lfsr >> 0) ^ (lfsr >> 2) ^ (lfsr >> 3) ^ (lfsr >> 5)) & 1;
        lfsr = (lfsr >> 1) | (bit << 15);

        /* Map LFSR to index 0..i */
        uint16_t j = lfsr % (i + 1);

        /* Swap */
        uint16_t tmp = sram_shuffle_buf[i];
        sram_shuffle_buf[i] = sram_shuffle_buf[j];
        sram_shuffle_buf[j] = tmp;
    }

    /* Step 3: Write shuffled values to AD9106 SRAM.
     * CALLER must enable MEM_ACCESS before and disable after.
     * This function ONLY writes SRAM data addresses (0x6002+). */
    for (uint16_t i = 0; i < count; i++)
    {
        AD9106_WriteRegister((uint16_t)(sram_shuffle_buf[i] << 4),
                             (uint16_t)(0x6002 + i));
    }
}

/**
 * @brief   Configure DDS for RAMP mode with given band parameters.
 *          Replicates the register setup from DDSSetConfiguration RAMP path.
 */
static void tdm_configure_dds_ramp(tsTdmBand* band)
{
    float bwHz = band->dds_bandwidth;
    float ctrl_freq = band->dds_ctrl_freq;

    /* ── Compute SRAM entries ──
     * RAMP: BW formula controls STOP_ADDR (proven, don't touch).
     * RANDOM: User Points controls STOP_ADDR — fewer = coarser = more energy per step. */
    uint16_t stop_addr;
    uint16_t active;
    if (band->dds_mode == TDM_DDS_MODE_RANDOM && band->dds_points >= 32)
    {
        uint32_t pts = band->dds_points;
        if (pts > 4096) pts = 4096;
        stop_addr = (uint16_t)(pts * 16);
        active = (uint16_t)pts;
    }
    else
    {
        stop_addr = tdm_compute_stop_addr(bwHz);
        active = stop_addr / 16;
        if (active < 32) active = 32;
    }

    /* ── ctrl_freq for multi-band TDM ──
     * The CTRL timer clocks the AD9106 SRAM pointer.  For per-band BW
     * control via STOP_ADDR, the pattern engine must wrap (hit STOP_ADDR)
     * at least once per dwell.  Since STOP_ADDR uses equality match (not ≥),
     * when hopping wide→narrow the pointer may overshoot, giving one
     * "transition sweep" at wrong BW.
     *
     * STRATEGY: Run ctrl_freq as FAST as possible (up to TIM4 max = 45 MHz).
     * Faster sweeps = shorter transition sweep = less BW error.
     * At 40 MHz, one full sweep = 4096/40MHz = 102µs.  Transition sweep
     * is at most 102µs regardless of dwell.  At 3ms dwell that's 3.4%.
     *
     * DO NOT make ctrl_freq dwell-dependent:
     *   - Short dwell (100µs) → 38 MHz → sweep = 100µs = entire dwell!
     *   - Long dwell (3ms) → 1.3 MHz → sweep = 3ms → awful transition
     *
     * Fixed high rate gives consistent behaviour at all dwell times.
     * C# single-band uses BW/10 (~8 MHz) — fine for non-hopping, but
     * too slow for TDM multi-band where transition error matters.
     *
     * If user explicitly set a per-band ctrl_freq via UI, respect it. */
    if (ctrl_freq > 0.0f && ctrl_freq <= 45000000.0f)
    {
        /* User override from FCtrl column (stored as Hz in EEPROM) */
    }
    else
    {
        ctrl_freq = 40000000.0f;  /* 40 MHz default — fast sweeps */
    }

    /* Set DDS sweep parameters */
    DeviceCfg.DDS->sweep_ctrl = 2;  /* AD9106_RAMP */
    DeviceCfg.DDS->begin = DeviceCfg.DDS->current = 0.0f;
    DeviceCfg.DDS->step  = bwHz / (float)active;
    DeviceCfg.DDS->points = active;
    DeviceCfg.DDS->ctrl_freq = ctrl_freq;

    /* ── Full DDS RAMP configuration ──
     * dms_init does NOT load DDS registers from EEPROM (AT24 return value
     * bug in original firmware — all reads fail silently). The DDS boots
     * with compiled config header defaults which are NOT RAMP mode.
     *
     * We must configure ALL RAMP registers here, matching what C#
     * DDSSetConfiguration() sends via SequenceSetRegisterDDS.
     *
     * Strategy: Update RAMP-specific values in shadow array, then write
     * ALL 96 registers from shadow to hardware via direct SPI. This is
     * the same as C#'s loop: for (i=0; i<0x60; i++) WriteRegister. */

    /* ── SRAM fill ──
     * CRITICAL DESIGN: Fill SRAM ONCE at startup with full 12-bit ramp (0→4095)
     * across the WIDEST band's points.  All bands reuse this SRAM.
     * BW is controlled by FTW register (set per-band), NOT by SRAM values.
     * Points controlled by STOP_ADDR (set per-band).
     *
     * RANDOM mode: same ramp values but in shuffled order.
     * IMPORTANT: SRAM is filled ONCE and NOT refilled during hopping.
     *
     * CRITICAL FIX: SRAM must have ALL 4096 entries filled because
     * the C# setRampBandwidth formula computes STOP_ADDR entries up to
     * 3855 (for 80MHz BW). If only 2000 entries are filled, wider BWs
     * read empty SRAM = no output for part of the sweep.
     */
    uint8_t use_random = 0;
    float widest_bw = bwHz;

    for (uint8_t i = 0; i < TdmCfg.active_count; i++)
    {
        tsTdmBand* b = &TdmCfg.bands[TdmCfg.active_list[i]];
        if (b->dds_mode == TDM_DDS_MODE_RANDOM)
            use_random = 1;
        if (b->dds_mode != TDM_DDS_MODE_PRBS)
        {
            if (b->dds_bandwidth > widest_bw)
                widest_bw = b->dds_bandwidth;
        }
    }
    if (band->dds_mode == TDM_DDS_MODE_RANDOM) use_random = 1;

    /* ── SRAM fill ──
     * RAMP: linear 0→4095, BW controlled by STOP_ADDR (reads fewer entries = lower values).
     * RANDOM: shuffle must be limited to BW-proportional range.
     * 170 MHz = full DAC range (4095). Linear scaling: maxRV = BW/170 × 4095 */
    uint16_t maxRampValue;
    uint16_t sram_fill_entries = 4096;

    if (use_random)
    {
        /* Simple linear: BW / 170MHz × 4095 = proportional DAC range.
         * For Random mode, TwMem is forced to 0 (no decimation) so
         * SRAM values map directly to frequency with no ÷4. */
        uint32_t rv = (uint32_t)((widest_bw / 170000000.0f) * 4095.0f);
        if (rv < 32) rv = 32;
        if (rv > 4095) rv = 4095;
        maxRampValue = (uint16_t)rv;
    }
    else
    {
        maxRampValue = 4095;
    }

    /* Store SRAM parameters */
    TdmCfg.sram_bw      = widest_bw;
    TdmCfg.sram_twMem   = 0;
    TdmCfg.sram_maxRV   = maxRampValue;
    TdmCfg.sram_entries  = sram_fill_entries;

    if (use_random)
    {
        AD9106_WriteRegister(0x0000, 0x1F);   /* Clear RUN */
        AD9106_WriteRegister(0x0004, 0x1E);   /* Enable MEM_ACCESS */
        AD9106_WriteRegister(0x0001, 0x1D);   /* Memory update */
        tdm_fill_sram_random(maxRampValue, sram_fill_entries);
        AD9106_WriteRegister(0x0001, 0x1D);   /* Memory update */
        AD9106_WriteRegister(0x0000, 0x1E);   /* Disable MEM_ACCESS */
        AD9106_WriteRegister(0x0001, 0x1D);   /* Memory update */
    }
    else
    {
        AD9106_RampConfig_WriteToMemory(maxRampValue, sram_fill_entries);
    }

    /* C#'s DDSSetConfiguration() sets RAMP-mode registers before writing.
     * We MUST do the same — the shadow may contain boot defaults (CONTINUE mode)
     * from ad9106_config1.h, not RAMP mode.  Without these, the AD9106 won't sweep. */

    /* ── FTW / TW_RAM — DO NOT OVERRIDE ──
     * C#'s setRampBandwidth() already configured 0x3E, 0x3F, 0x47 correctly
     * via USB before CMD_TDM_START. Those values are in the shadow array.
     * Every firmware attempt to override has failed (80/160 MHz output).
     * Just read what C# set and use it. */
    TdmCfg.sram_twMem = AD9106_GetRegister(0x47) & 0x0F;

    /* ── RAMP-mode register values — exact match of C# lines 674-699 ── */
    AD9106_SetRegister(0x20, 0x0001);       /* WAV_CONFIG: pattern from SRAM */
    AD9106_SetRegister(0x26, 0x3200);       /* PAT_TYPE CH4=RAMP(0x32), CH3=off */
    AD9106_SetRegister(0x27, 0x0000);       /* PAT_TYPE CH2=off, CH1=off */
    AD9106_SetRegister(0x28, 0x0111);       /* DAC_DGAIN */
    AD9106_SetRegister(0x29, 0x8000);       /* SAW_CONFIG */
    AD9106_SetRegister(0x2A, 0x0101);       /* DDS_TW32 */
    AD9106_SetRegister(0x2B, 0x0101);       /* DDS_TW1 */
    AD9106_SetRegister(0x2C, 0x0003);       /* DDS_PW */
    AD9106_SetRegister(0x2D, 0x0000);       /* TRIG_TW_SEL */
    AD9106_SetRegister(0x36, 0x0404);       /* SAW1_3CONFIG: SAW=4 for CH4+CH3 */
    AD9106_SetRegister(0x37, 0x0404);       /* SAW2_4CONFIG: SAW=4 for CH2+CH1 */
    AD9106_SetRegister(0x44, 0x0002);       /* PATTERN_DLY */
    AD9106_SetRegister(0x45, 0x4001);       /* PATTERN_PERIOD: CH4 prestore enable */

    /* Override STOP_ADDR/DDS_CYC in shadow — use user Points for STOP_ADDR */
    {
        /* TwMem: use EEPROM value if set (0-4), else auto-compute.
         * Random default=0, Ramp default=compute from BW. */
        uint16_t twMem;
        if (TdmCfg.eeprom_tw_ram <= 4)
            twMem = TdmCfg.eeprom_tw_ram;
        else if (band->dds_mode == TDM_DDS_MODE_RANDOM)
            twMem = 0;
        else
            twMem = tdm_compute_twmem(bwHz);
        AD9106_SetRegister(0x47, twMem);
        for (int i = 0; i < 4; i++)
        {
            AD9106_SetRegister((uint16_t)(0x50 + 4*i), 2);            /* START_DLY = 2 (was 1000) */
            AD9106_SetRegister((uint16_t)(0x51 + 4*i), 0x0000);       /* START_ADDR = 0 */
            AD9106_SetRegister((uint16_t)(0x52 + 4*i), stop_addr);    /* STOP_ADDR */
            AD9106_SetRegister((uint16_t)(0x53 + 4*i), 0xFFFF);       /* DDS_CYC = max */
        }
    }

    /* Write all registers from shadow to hardware — inline, not AD9106_Init(),
     * because AD9106_Init uses PAT_STATUS=0x0001 which works at boot (hardware
     * reset clears internal state) but NOT after SRAM fill.
     * C# DDSSetConfiguration RAMP path uses PAT_STATUS=0x0003 (bit 1 resets
     * SRAM read pointer).  We replicate that exact latch sequence here. */
    AD9106_WriteRegister(0x0000, 0x0000);   /* SPICONFIG — same as AD9106_Init */
    for (unsigned short ri = 0; ri < 0x60; ri++)
    {
        AD9106_WriteRegister(AD9106_GetRegister(ri), ri);
    }
    /* ── C# RAMP latch sequence (FmMain.cs line 705-709) ── */
    AD9106_WriteRegister(0x0003, 0x001E);   /* PAT_STATUS = RUN + bit1 (pattern reset) */
    AD9106_WriteRegister(0x0000, 0x001F);   /* Clear RAMUPDATE */
    AD9106_WriteRegister(0x0001, 0x001D);   /* MEM_UPDATE — latch everything */

    /* DDS trigger: 10 Hz for 500ms then stop (matches C# RAMP path) */
    tdm_dds_ctrl_config(1);
    pulse_timer_ctrl(&htim4, TIM_CHANNEL_3, PULSE_TIMER_ON, 10, 0.1);
    HAL_Delay(500);
    tdm_dds_ctrl_config(0);
    pulse_timer_ctrl(&htim4, TIM_CHANNEL_3, PULSE_TIMER_OFF, 0, 0);

    /* Manual trigger: RESET → SET → RESET */
    HAL_GPIO_WritePin(DDS_CTRL_GPIO_Port, DDS_CTRL_Pin, GPIO_PIN_RESET);
    for (volatile int i = 0; i < 10000; i++) {}
    HAL_GPIO_WritePin(DDS_CTRL_GPIO_Port, DDS_CTRL_Pin, GPIO_PIN_SET);
    for (volatile int i = 0; i < 10000; i++) {}
    HAL_GPIO_WritePin(DDS_CTRL_GPIO_Port, DDS_CTRL_Pin, GPIO_PIN_RESET);

    /* Start continuous DDS control clock — runs for entire TDM session */
    tdm_dds_ctrl_config(1);
    pulse_timer_ctrl(&htim4, TIM_CHANNEL_3, PULSE_TIMER_ON, ctrl_freq, 0.50);
    tdm_running_ctrl_freq = ctrl_freq;
    tdm_timer_running = 1;

    /* Track current bandwidth and points for change detection */
    TdmCfg.current_bw = bwHz;
    TdmCfg.current_points = band->dds_points;
}

/**
 * @brief   Configure DDS for PRBS (Pseudo-Random) noise mode.
 *          Exact match of C# DDSSetConfiguration() PSEUDO path.
 *
 *  BW control:
 *    BW ≤ 45 MHz → timer runs continuously at BW rate
 *    BW > 45 MHz → timer primes for 100ms then stops → full 170 MHz ref clock
 */
static void tdm_configure_dds_prbs(tsTdmBand* band)
{
    float ctrl_freq = band->dds_ctrl_freq;
    if (ctrl_freq <= 0) ctrl_freq = band->dds_bandwidth;

    DeviceCfg.DDS->sweep_ctrl = 3;  /* AD9106_PSEUDO */
    DeviceCfg.DDS->ctrl_freq = ctrl_freq;

    /* NO AD9106_Init() here — that resets to RAMP state and briefly
     * runs the pattern engine with RAMP config, confusing PSEUDO.
     * Boot already initialized shadow to compiled defaults. */

    /* ═══ Step 1: STOP pattern + clear status ═══
     * C# has ~1ms USB delay between each register write.
     * Add small delays to match. */
    AD9106_WriteRegister(0x0000, 0x1F);  /* Clear pattern status */
    HAL_Delay(1);
    AD9106_WriteRegister(0x0000, 0x1E);  /* STOP pattern engine */
    HAL_Delay(1);
    AD9106_WriteRegister(0x0001, 0x1D);  /* Memory update */
    HAL_Delay(1);

    /* ═══ Step 2: Set PSEUDO registers in shadow ═══
     * Boot already called AD9106_Init() which loaded defaults.
     * We only override the PSEUDO-specific registers. */
    AD9106_SetRegister(0x26, 0x2100);   /* CH4=PSEUDO(0x21), CH3=off(0x00) */
    AD9106_SetRegister(0x27, 0x0000);   /* CH2=off, CH1=off — only CH4 connected */
    AD9106_SetRegister(0x28, 0x0111);
    AD9106_SetRegister(0x29, 0x8000);
    AD9106_SetRegister(0x2A, 0x0101);
    AD9106_SetRegister(0x2B, 0x0101);
    AD9106_SetRegister(0x36, 0x0000);   /* No SAW (noise mode) */
    AD9106_SetRegister(0x37, 0x0000);
    AD9106_SetRegister(0x44, 0x0000);   /* No prestore */
    AD9106_SetRegister(0x45, 0x0000);

    for (int i = 0; i < 4; i++)
    {
        AD9106_SetRegister((uint16_t)(0x50 + 4*i), 0x0001);
        AD9106_SetRegister((uint16_t)(0x51 + 4*i), 0x0000);
        AD9106_SetRegister((uint16_t)(0x52 + 4*i), 0x0000);
        AD9106_SetRegister((uint16_t)(0x53 + 4*i), 0xFFFF);
    }

    /* ═══ Step 3: Write config registers 0x20-0x5F ═══ */
    for (uint16_t reg = 0x20; reg < 0x60; reg++)
    {
        AD9106_WriteRegister(AD9106_GetRegister(reg), reg);
    }

    /* ═══ Step 4: Latch sequence ═══ */
    AD9106_WriteRegister(0x0001, 0x1D);  /* Memory update */
    HAL_Delay(1);
    AD9106_WriteRegister(0x0000, 0x1F);  /* Clear status */
    HAL_Delay(1);
    AD9106_WriteRegister(0x0001, 0x1E);  /* START (RUN only, no MEM_ACCESS) */
    HAL_Delay(1);
    AD9106_WriteRegister(0x0001, 0x1D);  /* Memory update */
    HAL_Delay(1);

    /* ═══ Step 5: Start PRBS via CTRL timer ═══
     * AD9106 PSEUDO mode: LFSR advances on each CTRL clock edge.
     * Noise bandwidth = CTRL frequency.  Timer MUST keep running.
     * 
     * ctrl_freq = dds_ctrl_freq if user set it, else dds_bandwidth.
     * e.g. 40 MHz BW → 40 MHz CTRL → 40 MHz noise bandwidth.
     * TIM4 max ~45 MHz. */
    if (ctrl_freq <= 0 || ctrl_freq > 45000000.0f)
        ctrl_freq = 40000000.0f;

    tdm_dds_ctrl_config(1);
    pulse_timer_ctrl(&htim4, TIM_CHANNEL_3, PULSE_TIMER_ON, ctrl_freq, 0.50);
    tdm_running_ctrl_freq = ctrl_freq;
    tdm_timer_running = 1;

    TdmCfg.current_dds_mode = TDM_DDS_MODE_PRBS;
    TdmCfg.current_bw = band->dds_bandwidth;
}

/**
 * @brief   Lightweight DDS mode switch during TDM hopping.
 *          Only writes the registers that differ between RAMP/RANDOM and PRBS.
 *          Much faster than full configure (~15 SPI writes vs 96).
 *          RANDOM uses identical registers to RAMP (only SRAM content differs,
 *          and SRAM was filled at startup — not touched during hopping).
 *
 * @param   new_mode: TDM_DDS_MODE_RAMP, TDM_DDS_MODE_RANDOM, or TDM_DDS_MODE_PRBS
 * @param   band: Target band (for RAMP/RANDOM endpoint/BW config)
 */
static void tdm_switch_dds_mode(uint8_t new_mode, tsTdmBand* band)
{
    if (new_mode == TDM_DDS_MODE_PRBS)
    {
        /* ── RAMP/RANDOM → PRBS ── */
        DeviceCfg.DDS->sweep_ctrl = 3;  /* AD9106_PSEUDO */

        /* Stop pattern before changing mode */
        AD9106_WriteRegister(0x0000, 0x1E);  /* STOP */
        AD9106_WriteRegister(0x0001, 0x1D);  /* Update */

        /* Channel mode: RAMP(0x32) → PSEUDO(0x21) — CH4 only */
        AD9106_SetRegister(0x26, 0x2100);
        AD9106_SetRegister(0x27, 0x0000);
        AD9106_WriteRegister(0x2100, 0x26);
        AD9106_WriteRegister(0x0000, 0x27);

        /* Disable SAW and prestore (not used in PRBS) */
        AD9106_SetRegister(0x36, 0x0000);
        AD9106_SetRegister(0x37, 0x0000);
        AD9106_SetRegister(0x44, 0x0000);
        AD9106_SetRegister(0x45, 0x0000);
        AD9106_WriteRegister(0x0000, 0x36);
        AD9106_WriteRegister(0x0000, 0x37);
        AD9106_WriteRegister(0x0000, 0x44);
        AD9106_WriteRegister(0x0000, 0x45);

        /* Full SRAM range for PRBS */
        for (int i = 0; i < 4; i++)
        {
            AD9106_SetRegister((uint16_t)(0x52 + 4*i), 0x0000);
            AD9106_SetRegister((uint16_t)(0x53 + 4*i), 0xFFFF);
            AD9106_WriteRegister(0x0000, (uint16_t)(0x52 + 4*i));
            AD9106_WriteRegister(0xFFFF, (uint16_t)(0x53 + 4*i));
        }

        /* Restart pattern — RUN only, no MEM_ACCESS */
        AD9106_WriteRegister(0x0001, 0x1E);  /* START */
        AD9106_WriteRegister(0x0001, 0x1D);  /* Update */
    }
    else
    {
        /* ── PRBS → RAMP/RANDOM ── (same registers for both)
         * SRAM was pre-filled at TDM startup (tdm_start scans all bands
         * and fills SRAM with linear or shuffled ramp data before first hop).
         * No SRAM refill needed here — just switch registers. */
        DeviceCfg.DDS->sweep_ctrl = 2;  /* AD9106_RAMP */

        /* Channel mode: PSEUDO(0x21) → RAMP(0x32) */
        AD9106_SetRegister(0x26, 0x3232);
        AD9106_SetRegister(0x27, 0x3232);
        AD9106_WriteRegister(0x3232, 0x26);
        AD9106_WriteRegister(0x3232, 0x27);

        /* Re-enable SAW and prestore for RAMP */
        AD9106_SetRegister(0x36, 0x0404);
        AD9106_SetRegister(0x37, 0x0404);
        AD9106_SetRegister(0x44, 0x0002);
        AD9106_SetRegister(0x45, 0x4445);
        AD9106_WriteRegister(0x0404, 0x36);
        AD9106_WriteRegister(0x0404, 0x37);
        AD9106_WriteRegister(0x0002, 0x44);
        AD9106_WriteRegister(0x4445, 0x45);

        /* Restore DDS_CYC = 0xFFFF (max repeats) for all channels.
         * PRBS mode also uses 0xFFFF; RAMP needs same for continuous looping. */
        for (int i = 0; i < 4; i++)
        {
            AD9106_SetRegister((uint16_t)(0x53 + 4*i), 0xFFFF);
            AD9106_WriteRegister(0xFFFF, (uint16_t)(0x53 + 4*i));
        }

        /* Reconfigure RAMP endpoints for this band's bandwidth */
        tdm_reconfigure_dds_bandwidth(band);
    }

    /* Update ctrl_freq — use persistent startup rate or fixed 40 MHz */
    float ctrl_freq = band->dds_ctrl_freq;
    if (ctrl_freq <= 0)
    {
        ctrl_freq = (tdm_running_ctrl_freq > 0) ? tdm_running_ctrl_freq : 40000000.0f;
    }
    DeviceCfg.DDS->ctrl_freq = ctrl_freq;

    TdmCfg.current_dds_mode = new_mode;
}

/**
 * @brief   Compute TW_RAM_CONFIG (TwMem) for a given bandwidth.
 *          EXACT match of C# AD9106.setRampBandwidth() 3-level formula.
 *
 *  TwMem controls SRAM decimation in the AD9106 pattern engine.
 *  For typical AWJ bandwidths (20-180 MHz), TwMem is always 2.
 */
static uint16_t tdm_compute_twmem(float bwHz)
{
    double fRampMax = ((double)bwHz / (2.0 * 170000000.0)) * 16777216.0;
    if (fRampMax / 4.0 > 1500.0)
        return 2;
    else if (fRampMax / 2.0 > 1500.0)
        return 1;
    else
        return 0;
}

/**
 * @brief   Compute STOP_ADDR for a given bandwidth.
 *          EXACT match of C# setRampBandwidth().Points → DDSSetConfiguration STOP_ADDR.
 *
 *  C# formula:  Points = (uint)(fRampMax / divisor)
 *               STOP_ADDR = (ushort)(Points * 16)   ← intentional 16-bit truncation
 *
 *  This is the ACTUAL BW control mechanism. Different BWs with same TwMem
 *  produce different Points, which map to different STOP_ADDR values.
 *  The pattern engine reads SRAM from START_ADDR=0 to STOP_ADDR, so
 *  higher STOP_ADDR = more ramp entries read = wider BW.
 *
 *  Examples (verified against C# output):
 *    40 MHz → Points=493447, STOP_ADDR=30832, entry=1927
 *    80 MHz → Points=986895, STOP_ADDR=61680, entry=3855
 */
static uint16_t tdm_compute_stop_addr(float bwHz)
{
    double fRampMax = ((double)bwHz / (2.0 * 170000000.0)) * 16777216.0;
    uint32_t points;
    if (fRampMax / 4.0 > 1500.0)
        points = (uint32_t)(fRampMax / 4.0);
    else if (fRampMax / 2.0 > 1500.0)
        points = (uint32_t)(fRampMax / 2.0);
    else
        points = (uint32_t)fRampMax;
    return (uint16_t)(points * 16);  /* Intentional 16-bit truncation, matches C# (ushort) cast */
}

/**
 * @brief   Reconfigure DDS bandwidth mid-TDM when switching to a band
 *          with different bandwidth.
 *
 *  FAST PATH: Updates FTW (3 SPI) + STOP_ADDR (8 SPI) = ~30µs total.
 *  FTW register encodes the sweep bandwidth (set per-band).
 *  STOP_ADDR controls number of SRAM entries read (= Points).
 *  SRAM always contains full 0→4095 ramp (filled once at startup).
 *
 *  OLD approach refilled 4096 SRAM entries per hop (~4ms) — 40× the dwell!
 */
static void tdm_reconfigure_dds_bandwidth(tsTdmBand* band)
{
    float bwHz = band->dds_bandwidth;
    float ctrl_freq = band->dds_ctrl_freq;

    /* ══════════════════════════════════════════════════════════════════
     * Per-band BW control using C# setRampBandwidth formula.
     *
     * SRAM contains a linear ramp 0→4095 across all 4096 entries.
     * STOP_ADDR (from C# formula) controls how far the pattern engine
     * reads into SRAM = how high the ramp goes = BW scaling.
     * TwMem (register 0x47) controls SRAM decimation.
     *
     *   40 MHz → STOP_ADDR=30832, entry=1927 → ramp peaks at ~1927
     *   80 MHz → STOP_ADDR=61680, entry=3855 → ramp peaks at ~3855
     * ══════════════════════════════════════════════════════════════════ */

    uint16_t stop_addr;
    uint16_t entries;
    if (band->dds_mode == TDM_DDS_MODE_RANDOM && band->dds_points >= 32)
    {
        uint32_t pts = band->dds_points;
        if (pts > 4096) pts = 4096;
        stop_addr = (uint16_t)(pts * 16);
        entries = (uint16_t)pts;
    }
    else
    {
        stop_addr = tdm_compute_stop_addr(bwHz);
        entries   = stop_addr / 16;
        if (entries < 32) entries = 32;
    }
    uint16_t twMem;
    if (TdmCfg.eeprom_tw_ram <= 4)
        twMem = TdmCfg.eeprom_tw_ram;
    else if (band->dds_mode == TDM_DDS_MODE_RANDOM)
        twMem = 0;
    else
        twMem = tdm_compute_twmem(bwHz);

    /* Compute ctrl_freq: Use persistent tdm_running_ctrl_freq (40 MHz fixed rate). */
    if (ctrl_freq <= 0)
    {
        ctrl_freq = (tdm_running_ctrl_freq > 0) ? tdm_running_ctrl_freq : 40000000.0f;
    }

    /* Update device config */
    DeviceCfg.DDS->begin = DeviceCfg.DDS->current = 0.0f;
    DeviceCfg.DDS->step  = bwHz / (float)entries;
    DeviceCfg.DDS->points = entries;
    DeviceCfg.DDS->ctrl_freq = ctrl_freq;

    /* ── Write TwMem + STOP_ADDR + DDS_CYC to hardware ──
     * NO MEM_UPDATE here — latch_and_trigger handles all latching. */
    AD9106_SetRegister(0x47, twMem);
    AD9106_WriteRegister(twMem, 0x47);

    for (int i = 0; i < 4; i++)
    {
        AD9106_SetRegister((uint16_t)(0x52 + 4*i), stop_addr);
        AD9106_WriteRegister(stop_addr, (uint16_t)(0x52 + 4*i));
        AD9106_WriteRegister(0xFFFF,    (uint16_t)(0x53 + 4*i));
    }

    TdmCfg.current_bw = bwHz;
}

/**
 * @brief   Hop to a specific band.
 *
 *  PRBS→PRBS: PLL-only fast path (~10µs), no DDS touch.
 *  Everything else: Original proven RAMP path.
 */
static void tdm_hop_to_band(uint8_t band_idx)
{
    tsTdmBand* band = &TdmCfg.bands[band_idx];

    /* ── K9: Per-band attenuation ──
     * Apply this band's own attenuator setting on every hop.
     * 0xFFFF or 0 = "no per-band value set" → fall back to the main
     * attenuator (Position 0). Runs before every fast-path return below,
     * so it applies in all modes. HAL_DAC_SetValue is an internal register
     * write (~sub-µs), so it does not disturb hot-hop timing. */
    {
        uint16_t atten = band->atten_dac;
        if (atten == 0xFFFF || atten == 0)
            atten = DeviceCfg.Attenuator.Dac[0];
        HAL_DAC_SetValue(&hdac, DAC1_CHANNEL_1, DAC_ALIGN_12B_R, atten);
    }

    /* ── PRBS fast path: move PLL + update timer if BW differs ── */
    if (band->dds_mode == TDM_DDS_MODE_PRBS &&
        TdmCfg.current_dds_mode == TDM_DDS_MODE_PRBS)
    {
        tdm_pll_fast_hop(band_idx);

        /* Update CTRL timer if this band has different BW (= different noise BW) */
        float target_freq = band->dds_ctrl_freq;
        if (target_freq <= 0) target_freq = band->dds_bandwidth;
        if (target_freq <= 0 || target_freq > 45000000.0f) target_freq = 40000000.0f;

        if (target_freq != tdm_running_ctrl_freq)
        {
            pulse_timer_ctrl(&htim4, TIM_CHANNEL_3, PULSE_TIMER_ON, target_freq, 0.50);
            tdm_running_ctrl_freq = target_freq;
        }

        TdmCfg.current_band = band_idx;
        return;
    }

    /* ── RAMP/RANDOM → RAMP/RANDOM: HOT HOP ──
     * CTRL timer runs continuously — NEVER stopped between same-mode hops.
     * Pattern engine loops START_ADDR→STOP_ADDR continuously (DDS_CYC=0xFFFF).
     *
     * Each hop does ONLY:
     *  1. PLL SPI change (~10µs, DDS still sweeping at old freq = not silence)
     *  2. Write per-band STOP_ADDR to hardware via SPI
     *  3. MEM_UPDATE latch — pattern engine picks up new STOP_ADDR on next wrap
     *
     * NO PAT_STATUS reset — this was stopping the pattern engine and causing
     * 50%+ dropout.  Without reset, the current sweep finishes at the old
     * STOP_ADDR, then the next sweep uses the new one.  One transition
     * sweep may have wrong BW — acceptable vs total dropout.
     *
     * Total hop time: ~15µs (PLL + 5 SPI writes).  DDS output is
     * continuous throughout — zero dead time. */
    if (band->dds_mode != TDM_DDS_MODE_PRBS &&
        TdmCfg.current_dds_mode != TDM_DDS_MODE_PRBS &&
        tdm_timer_running)
    {
        /* 1. PLL change — DDS still sweeping at old frequency during this */
        tdm_pll_fast_hop(band_idx);

        /* 2. Write per-band STOP_ADDR to all 4 channels via direct SPI.
         *    RANDOM: use user Points. RAMP: use BW formula. */
        uint16_t stop_addr;
        if (band->dds_mode == TDM_DDS_MODE_RANDOM && band->dds_points >= 32)
        {
            uint32_t pts = band->dds_points;
            if (pts > 4096) pts = 4096;
            stop_addr = (uint16_t)(pts * 16);
        }
        else
        {
            stop_addr = tdm_compute_stop_addr(band->dds_bandwidth);
        }
        for (int i = 0; i < 4; i++)
            AD9106_WriteRegister(stop_addr, (uint16_t)(0x52 + 4*i));

        /* TwMem: use EEPROM value if set, else auto. Only write if changed. */
        uint16_t twMem;
        if (TdmCfg.eeprom_tw_ram <= 4)
            twMem = TdmCfg.eeprom_tw_ram;
        else if (band->dds_mode == TDM_DDS_MODE_RANDOM)
            twMem = 0;
        else
            twMem = tdm_compute_twmem(band->dds_bandwidth);
        if (twMem != (AD9106_GetRegister(0x47) & 0x0F))
        {
            AD9106_SetRegister(0x47, twMem);
            AD9106_WriteRegister(twMem, 0x47);
        }

        /* 3. MEM_UPDATE ONLY — latch new STOP_ADDR to active registers.
         *    Pattern engine continues running, picks up new STOP_ADDR
         *    on its next START_ADDR→STOP_ADDR cycle wrap.
         *    NO PAT_STATUS RESET — that kills the pattern engine. */
        AD9106_WriteRegister(0x0001, 0x1D);   /* MEM_UPDATE */

        /* Per-band CTRL frequency — update timer if user set custom FCtrl.
         * Only changes timer when value differs. 0 = keep current rate. */
        if (band->dds_ctrl_freq > 0.0f && band->dds_ctrl_freq <= 45000000.0f
            && band->dds_ctrl_freq != tdm_running_ctrl_freq)
        {
            pulse_timer_ctrl(&htim4, TIM_CHANNEL_3, PULSE_TIMER_ON,
                             band->dds_ctrl_freq, 0.50);
            tdm_running_ctrl_freq = band->dds_ctrl_freq;
        }

        /* Refresh DDS_CYC on every hop while engine is still running.
         * This was working in the "no gaps" version — keeps counter topped up. */
        for (int i = 0; i < 4; i++)
            AD9106_WriteRegister(0xFFFF, (uint16_t)(0x53 + 4*i));
        AD9106_WriteRegister(0x0001, 0x1D);   /* MEM_UPDATE to latch */

        TdmCfg.current_bw = band->dds_bandwidth;
        TdmCfg.current_band = band_idx;
        return;
    }

    /* ── MODE SWITCH path (PRBS ↔ RAMP/RANDOM) — needs full reconfigure ── */

    /* Stop DDS control timer before mode switch */
    pulse_timer_ctrl(&htim4, TIM_CHANNEL_3, PULSE_TIMER_OFF, 0, 0);
    tdm_dds_ctrl_config(0);
    tdm_timer_running = 0;

    /* Hop PLL to new center frequency (fast — pre-computed regs) */
    tdm_pll_fast_hop(band_idx);

    /* Full mode switch */
    tdm_switch_dds_mode(band->dds_mode, band);

    /* Re-trigger DDS engine + restart timer with correct per-band freq */
    float ctrl_freq = band->dds_ctrl_freq;
    if (band->dds_mode == TDM_DDS_MODE_PRBS)
    {
        /* PRBS: ctrl_freq = noise BW. Default to band's BW setting. */
        if (ctrl_freq <= 0) ctrl_freq = band->dds_bandwidth;
        if (ctrl_freq <= 0 || ctrl_freq > 45000000.0f) ctrl_freq = 40000000.0f;
        /* PRBS just needs the timer running — no RAMP latch sequence */
        tdm_dds_ctrl_config(1);
        pulse_timer_ctrl(&htim4, TIM_CHANNEL_3, PULSE_TIMER_ON, ctrl_freq, 0.50);
    }
    else
    {
        /* RAMP/RANDOM: use running rate or 40MHz default */
        if (ctrl_freq <= 0) ctrl_freq = 40000000.0f;
        DeviceCfg.DDS->ctrl_freq = ctrl_freq;
        tdm_dds_latch_and_trigger(ctrl_freq);
    }
    tdm_running_ctrl_freq = ctrl_freq;
    tdm_timer_running = 1;

    /* Track current band */
    TdmCfg.current_band = band_idx;
}

/**
 * @brief   ORIGINAL PROVEN latch + trigger + timer restart.
 *          DO NOT ADD PRBS BRANCHING HERE — this broke RAMP twice.
 *          PRBS never calls this during hopping (fast path returns early).
 */
static void tdm_dds_latch_and_trigger(float ctrl_freq)
{
    /* Stop timer first so CTRL pin is free for manual trigger */
    pulse_timer_ctrl(&htim4, TIM_CHANNEL_3, PULSE_TIMER_OFF, 0, 0);
    tdm_dds_ctrl_config(0);  /* Switch pin back to GPIO mode */

    /* ── Refresh DDS_CYC before every trigger ──
     * The AD9106 cycle counter decrements from DDS_CYC to 0 then stops.
     * Re-writing 0xFFFF ensures a fresh count on each trigger.
     * Without this, the counter eventually expires (~6.5s) and the
     * pattern engine halts permanently until chip is reconfigured. */
    for (int i = 0; i < 4; i++)
    {
        AD9106_WriteRegister(0xFFFF, (uint16_t)(0x53 + 4*i));  /* DDS_CYC = max */
    }

    /* AD9106 register latch — match C# RAMP mode exactly.
     * PAT_STATUS = 0x0003: bit 0 = RUN, bit 1 = reset SRAM read pointer.
     * Without bit 1, pattern engine may not restart from address 0. */
    AD9106_WriteRegister(0x0003, 0x1E);  /* PAT_STATUS = RUN + pattern reset */
    AD9106_WriteRegister(0x0000, 0x1F);  /* Clear RAMUPDATE */
    AD9106_WriteRegister(0x0001, 0x1D);  /* MEM_UPDATE */

    /* Manual trigger pulse — reduced from 2000/2000/1000 iterations.
     * AD9106 CTRL pin needs ~20ns setup/hold (datasheet).
     * 50 iterations at 180MHz ≈ 1µs — still 50× over spec.
     * Old 2000 iterations wasted ~40µs per edge = 120µs total. */
    HAL_GPIO_WritePin(DDS_CTRL_GPIO_Port, DDS_CTRL_Pin, GPIO_PIN_RESET);
    for (volatile int i = 0; i < 50; i++) {}

    HAL_GPIO_WritePin(DDS_CTRL_GPIO_Port, DDS_CTRL_Pin, GPIO_PIN_SET);
    for (volatile int i = 0; i < 50; i++) {}

    HAL_GPIO_WritePin(DDS_CTRL_GPIO_Port, DDS_CTRL_Pin, GPIO_PIN_RESET);
    for (volatile int i = 0; i < 25; i++) {}

    /* Restart FreqCtrl timer */
    if (ctrl_freq > 0)
    {
        tdm_dds_ctrl_config(1);
        pulse_timer_ctrl(&htim4, TIM_CHANNEL_3, PULSE_TIMER_ON, ctrl_freq, 0.50);
    }
}

/**
 * @brief   FAST DDS retrigger for single-band mode.
 *          Skips PLL (same freq), BW check, mode check.
 *          Only refreshes DDS_CYC + latch + trigger + timer restart.
 *          ~25µs vs ~75µs for full tdm_hop_to_band().
 */
static void tdm_dds_retrigger(float ctrl_freq)
{
    pulse_timer_ctrl(&htim4, TIM_CHANNEL_3, PULSE_TIMER_OFF, 0, 0);
    tdm_dds_ctrl_config(0);

    /* Refresh DDS_CYC — prevents 6.5s expiry */
    for (int i = 0; i < 4; i++)
        AD9106_WriteRegister(0xFFFF, (uint16_t)(0x53 + 4*i));

    /* Latch + trigger — C# RAMP mode uses PAT_STATUS = 0x0003 */
    AD9106_WriteRegister(0x0003, 0x1E);  /* PAT_STATUS = RUN + pattern reset */
    AD9106_WriteRegister(0x0000, 0x1F);  /* Clear RAMUPDATE */
    AD9106_WriteRegister(0x0001, 0x1D);  /* MEM_UPDATE */

    HAL_GPIO_WritePin(DDS_CTRL_GPIO_Port, DDS_CTRL_Pin, GPIO_PIN_RESET);
    for (volatile int i = 0; i < 50; i++) {}
    HAL_GPIO_WritePin(DDS_CTRL_GPIO_Port, DDS_CTRL_Pin, GPIO_PIN_SET);
    for (volatile int i = 0; i < 50; i++) {}
    HAL_GPIO_WritePin(DDS_CTRL_GPIO_Port, DDS_CTRL_Pin, GPIO_PIN_RESET);
    for (volatile int i = 0; i < 25; i++) {}

    /* Restart timer */
    tdm_dds_ctrl_config(1);
    pulse_timer_ctrl(&htim4, TIM_CHANNEL_3, PULSE_TIMER_ON, ctrl_freq, 0.50);
}

/**
 * @brief   Configure DDS_CTRL pin as timer output (1) or GPIO (0).
 *          Matches dms_dds_ctrl_config() in dms_functions.c.
 */
static void tdm_dds_ctrl_config(int flag)
{
    GPIO_InitTypeDef GPIO_InitStruct = {0};

    GPIO_InitStruct.Pin = DDS_CTRL_Pin;
    if (flag == 0)
    {
        GPIO_InitStruct.Mode  = GPIO_MODE_OUTPUT_PP;
        GPIO_InitStruct.Pull  = GPIO_NOPULL;
        GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
    }
    else
    {
        GPIO_InitStruct.Mode      = GPIO_MODE_AF_PP;
        GPIO_InitStruct.Pull      = GPIO_NOPULL;
        GPIO_InitStruct.Speed     = GPIO_SPEED_FREQ_LOW;
        GPIO_InitStruct.Alternate = GPIO_AF2_TIM4;
    }
    HAL_GPIO_Init(DDS_CTRL_GPIO_Port, &GPIO_InitStruct);
}

/**
 * @brief   DDS trigger sequence to restart RAMP output after silent phase.
 *          Uses the full latch + trigger sequence (CRITICAL FIX).
 */
static void tdm_dds_trigger_sequence(void)
{
    /* Full latch + trigger + timer restart */
    tdm_dds_latch_and_trigger(DeviceCfg.DDS->ctrl_freq);
}

/**
 * @brief   Kill DDS output (for pulsed mode silent phase or stop).
 */
static void tdm_kill_dds_output(void)
{
    /* Stop DDS control timer */
    pulse_timer_ctrl(&htim4, TIM_CHANNEL_3, PULSE_TIMER_OFF, 0, 0);
    tdm_dds_ctrl_config(0);

    /* Hold CTRL pin low - DDS stops ramping */
    HAL_GPIO_WritePin(DDS_CTRL_GPIO_Port, DDS_CTRL_Pin, GPIO_PIN_RESET);
}

/**
 * @brief   Set bicolour LED to a specific colour.
 *          Green = LED2 (PC9), Red = LED3 (PA8), Amber = both.
 */
static void tdm_set_led(uint8_t colour)
{
    switch (colour)
    {
        case LED_GREEN:
            HAL_GPIO_WritePin(LED2_GPIO_Port, LED2_Pin, GPIO_PIN_SET);
            HAL_GPIO_WritePin(LED3_GPIO_Port, LED3_Pin, GPIO_PIN_RESET);
            break;
        case LED_RED:
            HAL_GPIO_WritePin(LED2_GPIO_Port, LED2_Pin, GPIO_PIN_RESET);
            HAL_GPIO_WritePin(LED3_GPIO_Port, LED3_Pin, GPIO_PIN_SET);
            break;
        case LED_AMBER:
            HAL_GPIO_WritePin(LED2_GPIO_Port, LED2_Pin, GPIO_PIN_SET);
            HAL_GPIO_WritePin(LED3_GPIO_Port, LED3_Pin, GPIO_PIN_SET);
            break;
        default:  /* LED_OFF */
            HAL_GPIO_WritePin(LED2_GPIO_Port, LED2_Pin, GPIO_PIN_RESET);
            HAL_GPIO_WritePin(LED3_GPIO_Port, LED3_Pin, GPIO_PIN_RESET);
            break;
    }
}

/**
 * @brief   LED status handler. Call this INSTEAD OF dms_led_blinking().
 *
 *  Colour/Pattern key:
 *    Green slow blink (1Hz)     = Idle, healthy, ready
 *    Green solid                = USB connected & communicating
 *    Amber fast blink (10Hz)    = TDM jamming, continuous mode
 *    Amber pulse (on/off)       = TDM jamming, pulsed mode (follows pulse)
 *    Red slow blink (1Hz)       = PLL lock lost
 *    Red fast blink (5Hz)       = No active bands / config error
 *    Red solid                  = EEPROM fault at boot
 */
void tdm_led_update(void)
{
    uint32_t now = HAL_GetTick();
    uint32_t elapsed = now - led_last_tick;

    /* ── FAULT STATES (Red) ── take priority ── */
    if (tdm_fault != TDM_FAULT_NONE)
    {
        switch (tdm_fault)
        {
            case TDM_FAULT_EEPROM:
                /* Red solid */
                tdm_set_led(LED_RED);
                return;

            case TDM_FAULT_PLL:
                /* Red slow blink 1Hz (500ms on, 500ms off) */
                if (elapsed >= 500)
                {
                    led_last_tick = now;
                    led_toggle = !led_toggle;
                }
                tdm_set_led(led_toggle ? LED_RED : LED_OFF);
                return;

            case TDM_FAULT_NO_BANDS:
            case TDM_FAULT_DDS:
                /* Red fast blink 5Hz (100ms on, 100ms off) */
                if (elapsed >= 100)
                {
                    led_last_tick = now;
                    led_toggle = !led_toggle;
                }
                tdm_set_led(led_toggle ? LED_RED : LED_OFF);
                return;
        }
    }

    /* ── TDM JAMMING (Amber) ── */
    if (TdmCfg.state == TDM_STATE_RUNNING)
    {
        if (TdmCfg.mode == TDM_MODE_PULSED)
        {
            /* Amber on during jam, off during silent */
            tdm_set_led(LED_AMBER);
        }
        else
        {
            /* Continuous mode: amber fast blink 10Hz */
            if (elapsed >= 50)
            {
                led_last_tick = now;
                led_toggle = !led_toggle;
            }
            tdm_set_led(led_toggle ? LED_AMBER : LED_OFF);
        }
        return;
    }

    if (TdmCfg.state == TDM_STATE_SILENT)
    {
        /* Pulsed mode silent phase: LED off */
        tdm_set_led(LED_OFF);
        return;
    }

    /* ── IDLE (Green) ── */
    /* Green slow blink 1Hz */
    if (elapsed >= 500)
    {
        led_last_tick = now;
        led_toggle = !led_toggle;
    }
    tdm_set_led(led_toggle ? LED_GREEN : LED_OFF);
}

/********************  END OF FILE  *****************************************/
