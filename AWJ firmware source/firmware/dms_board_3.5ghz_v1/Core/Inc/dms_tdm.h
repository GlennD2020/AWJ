/****************************************************************************
 * @file:       dms_tdm.h
 * @project:    DMS - Time Division Multiplexed Jamming
 * @author:     K9 Electronics Ltd
 * @build:      2026-09-18a  (per-band atten_dac field in TDM band struct)
 * @brief:      Standalone TDM multi-band frequency hopping.
 *              Enables autonomous multi-band jamming without PC connection.
 *              Bands configured via USB, stored in EEPROM, executed on STM32.
 ****************************************************************************/
#ifndef __H_DMS_TDM_
#define __H_DMS_TDM_

/****************************************************************************
 *                      INCLUDES
 ****************************************************************************/
#ifdef STM32F446xx
    #include "stm32f4xx_hal.h"
#endif

#include <stdint.h>

/****************************************************************************
 *                      DEFINES
 ****************************************************************************/
/** Maximum number of bands in TDM table */
#define TDM_MAX_BANDS           8

/** EEPROM storage base address for TDM config (after existing data at 416) */
#define TDM_EEPROM_BASE         448
/** EEPROM: TDM header (mode, num_bands, dwell, pulse params) */
#define TDM_EEPROM_HEADER       TDM_EEPROM_BASE
/** EEPROM: Band table starts here (24 bytes per band) */
#define TDM_EEPROM_BANDS        (TDM_EEPROM_BASE + 32)

/** TDM operating modes */
#define TDM_MODE_OFF            0
#define TDM_MODE_CONTINUOUS     1
#define TDM_MODE_PULSED         2

/** TDM state */
#define TDM_STATE_IDLE          0
#define TDM_STATE_RUNNING       1
#define TDM_STATE_SILENT        2   /* Pulsed mode: in OFF phase */

/** Per-band DDS mode */
#define TDM_DDS_MODE_RAMP       0   /* AD9106 RAMP sweep (default) */
#define TDM_DDS_MODE_PRBS       1   /* AD9106 PSEUDO-random noise */
#define TDM_DDS_MODE_RANDOM     2   /* AD9106 RAMP registers, shuffled SRAM */

/** Minimum dwell time in microseconds (PLL lock settling) */
#define TDM_MIN_DWELL_US        50

/** DWT microsecond timer macros (ARM Cortex-M4 cycle counter) */
#define DWT_INIT() do { \
    CoreDebug->DEMCR |= CoreDebug_DEMCR_TRCENA_Msk; \
    DWT->CYCCNT = 0; \
    DWT->CTRL |= DWT_CTRL_CYCCNTENA_Msk; \
} while(0)
#define DWT_CYCLES()    (DWT->CYCCNT)
#define DWT_US(cycles)  ((cycles) / (SystemCoreClock / 1000000U))

/*  CMD_TDM_START (44), CMD_TDM_STOP (45), CMD_TDM_STATUS (46) are already
 *  in the syscom.h enum. The rest are NOT, so we define them here:        */
#ifndef CMD_SET_TDM_BAND
#define CMD_SET_TDM_BAND        40
#endif
#ifndef CMD_GET_TDM_BAND
#define CMD_GET_TDM_BAND        41
#endif
#ifndef CMD_SET_TDM_PARAMS
#define CMD_SET_TDM_PARAMS      42
#endif
#ifndef CMD_GET_TDM_PARAMS
#define CMD_GET_TDM_PARAMS      43
#endif
/* 44, 45, 46 defined in syscom.h enum — do NOT redefine here */
#ifndef CMD_TDM_SAVE_EEPROM
#define CMD_TDM_SAVE_EEPROM     47
#endif
#ifndef CMD_TDM_LOAD_EEPROM
#define CMD_TDM_LOAD_EEPROM     48
#endif

/****************************************************************************
 *                      TYPEDEFS
 ****************************************************************************/
/**
 * @brief   Single band configuration for TDM hopping.
 */
#pragma pack(push,1)
typedef struct
{
    double      lo_frequency;       /**< PLL center frequency in Hz (e.g. 2.405e9) */
    float       dds_bandwidth;      /**< DDS sweep bandwidth in Hz (e.g. 10e6) */
    float       dds_step;           /**< DDS frequency step in Hz */
    uint32_t    dds_points;         /**< Number of DDS sweep points */
    float       dds_ctrl_freq;      /**< DDS control clock frequency in Hz */
    uint8_t     active;             /**< 1=include in TDM cycle, 0=skip */
    uint8_t     dds_mode;           /**< TDM_DDS_MODE_RAMP(0), PRBS(1), or RANDOM(2) */
    uint16_t    atten_dac;          /**< Per-band attenuator DAC code (0xFFFF or 0 = use main attenuator). Was reserved[2]; same 2 bytes, offset 26. */
} tsTdmBand;
#pragma pack(pop)

/**
 * @brief   TDM runtime configuration and state.
 */
typedef struct
{
    /* ── Configuration (set by user, saved to EEPROM) ── */
    uint8_t     mode;               /**< TDM_MODE_OFF / CONTINUOUS / PULSED */
    uint8_t     num_bands;          /**< Number of bands configured (0..TDM_MAX_BANDS) */
    uint16_t    dwell_us;           /**< Dwell time per band in MICROSECONDS */
    uint16_t    pulse_on_ms;        /**< Pulsed mode: ON duration in ms */
    uint16_t    pulse_off_ms;       /**< Pulsed mode: OFF duration in ms */
    uint8_t     duty_percent;       /**< SRAM duty cycle 5-100%. Lower = cooler PAs.
                                         Controls what fraction of 4096 SRAM entries
                                         have ramp data vs zeros.  0 = treat as 100
                                         for backward compat with old EEPROM. */

    /* ── Band table ── */
    tsTdmBand   bands[TDM_MAX_BANDS];

    /* ── Runtime state (not saved) ── */
    uint8_t     state;              /**< TDM_STATE_xxx */
    uint8_t     current_band;       /**< Index of current active band */
    uint8_t     active_count;       /**< Number of active bands (cached) */
    uint8_t     active_list[TDM_MAX_BANDS]; /**< Indices of active bands */
    uint8_t     active_idx;         /**< Current position in active_list */
    uint32_t    last_hop_tick;      /**< HAL_GetTick() of last frequency hop (ms, for pulse mode) */
    uint32_t    last_hop_cyc;       /**< DWT->CYCCNT at last hop (for µs dwell timing) */
    uint32_t    pulse_tick;         /**< HAL_GetTick() of last pulse phase change */
    uint32_t    hop_count;          /**< Total hops since start (for diagnostics) */
    double      saved_lo_freq;      /**< Saved PLL frequency before TDM start */
    uint16_t    saved_dds_mode;     /**< Saved DDS mode before TDM start */
    float       saved_dds_begin;    /**< Saved DDS begin freq */
    float       saved_dds_step;     /**< Saved DDS step */
    uint32_t    saved_dds_points;   /**< Saved DDS points */
    float       saved_dds_ctrl;     /**< Saved DDS ctrl freq */
    float       current_bw;         /**< Current DDS bandwidth (for change detection) */
    uint32_t    current_points;     /**< Current DDS points (for change detection) */
    uint8_t     current_dds_mode;   /**< Current DDS mode (for RAMP↔PRBS switching) */
    float       sram_bw;            /**< BW (Hz) SRAM was filled for (widest active band) */
    uint16_t    sram_twMem;         /**< TW_RAM_CONFIG used for SRAM fill */
    uint16_t    sram_maxRV;         /**< MaxRampValue in SRAM ramp endpoint */
    uint16_t    sram_entries;       /**< Number of SRAM entries filled */
    uint8_t     eeprom_tw_ram;      /**< TW_RAM_CONFIG from C# library (EEPROM header v4+) */
    uint32_t    eeprom_ftw;         /**< FTW from C# library (EEPROM header v4+, 24-bit) */
    uint16_t    active_sram;        /**< Computed: (4096 * duty_percent) / 100. Num SRAM
                                         entries with ramp data; rest are zero (PA off) */
} tsTdmConfig;

/** LED status colours (bicolour common cathode: Green=LED2/PC9, Red=LED3/PA8) */
#define LED_OFF             0
#define LED_GREEN           1
#define LED_RED             2
#define LED_AMBER           3   /* Both on = amber/yellow */

/** LED fault codes (stored in tdm_fault) */
#define TDM_FAULT_NONE      0
#define TDM_FAULT_PLL       1   /* PLL not locked */
#define TDM_FAULT_NO_BANDS  2   /* TDM started but no active bands */
#define TDM_FAULT_EEPROM    3   /* EEPROM read failed at boot */
#define TDM_FAULT_DDS       4   /* DDS configuration error */

/****************************************************************************
 *                      EXTERN VARIABLES
 ****************************************************************************/
extern tsTdmConfig TdmCfg;

/****************************************************************************
 *                      FUNCTION DECLARATIONS
 ****************************************************************************/
/**
 * @brief   Initialize TDM module. Call once at startup.
 */
void tdm_init(void);

/**
 * @brief   TDM process function. Call from main loop EVERY iteration.
 *          When TDM is idle, returns immediately (~0 overhead).
 *          When TDM is running, handles frequency hopping.
 */
void tdm_process(void);

/**
 * @brief   Start TDM hopping. Configures DDS for first band's mode and begins.
 *          Supports per-band RAMP/PRBS mode switching during hopping.
 * @retval  0 = success, -1 = no active bands, -2 = already running
 */
int tdm_start(void);

/**
 * @brief   Stop TDM hopping. Restores previous hardware state.
 */
void tdm_stop(void);

/**
 * @brief   Configure a band in the TDM table.
 * @param   index: Band index (0..TDM_MAX_BANDS-1)
 * @param   band: Pointer to band configuration
 * @retval  0 = success, -1 = invalid index
 */
int tdm_set_band(uint8_t index, tsTdmBand* band);

/**
 * @brief   Set TDM operating parameters.
 * @param   mode: TDM_MODE_xxx
 * @param   dwell_us: Dwell time per band in MICROSECONDS (min 50)
 * @param   pulse_on_ms: Pulse ON time (pulsed mode only)
 * @param   pulse_off_ms: Pulse OFF time (pulsed mode only)
 * @param   duty_percent: SRAM duty cycle 5-100% (0=100% backward compat)
 */
void tdm_set_params(uint8_t mode, uint16_t dwell_us,
                    uint16_t pulse_on_ms, uint16_t pulse_off_ms,
                    uint8_t duty_percent);

/**
 * @brief   Process TDM-related USB commands.
 *          Called from dms_decode_receive_command() for cmd types 40-48.
 * @param   pRxCmd: Pointer to received command
 * @param   pTxCmd: Pointer to response buffer
 * @retval  1 = command handled, 0 = not a TDM command
 */
int tdm_process_command(void* pRxCmd, void* pTxCmd);

/**
 * @brief   Save TDM configuration to EEPROM.
 * @retval  0 = success, -1 = write error
 */
int tdm_save_to_eeprom(void);

/**
 * @brief   Load TDM configuration from EEPROM.
 * @retval  0 = success, -1 = read error, -2 = CRC mismatch
 */
int tdm_load_from_eeprom(void);

/**
 * @brief   Check hardware ENABLE pin state for start/stop trigger.
 *          Call from thread2 (e.g. in dms_check_switch_state).
 */
void tdm_check_enable_pin(void);

/**
 * @brief   LED status handler. REPLACES dms_led_blinking().
 *          Call from thread2 at the same point where dms_led_blinking was.
 *          Green = healthy, Amber = jamming, Red = fault.
 */
void tdm_led_update(void);

#endif  /* __H_DMS_TDM_ */

/************************** END OF FILE *************************************/
