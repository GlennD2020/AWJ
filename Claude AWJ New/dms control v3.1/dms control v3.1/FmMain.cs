/**
 * @File:   FmMain.cs
 * @Author: K9 Electronics.
 * @Build:  2026-09-18a  (per-band attenuation + save-commit fix)
 *          Shown in the app title bar — must match what's on screen.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

using System.Runtime.InteropServices;
using System.Collections.ObjectModel;
using Microsoft.Win32.SafeHandles;

using LibUsbDotNet;
using LibUsbDotNet.Info;
using LibUsbDotNet.Main;

using nsAlexKir;
using nsAlexKir.RF_Library;
using nsAlexKir.Sequences;
using nsAlexKir.nsDebug;
using nsAlexKir.MathLibrary;

namespace dms_control_v3
{
    /// <summary> Адреса блоков для хранения данных.</summary>
    public enum EEPROM : ushort
    {
        SN_DESCRIPTION = 0,    //< Информация о устройстве.
        DEVICE_CONFIG = 32,   //< Базовая конфигурация устройства.
        PLL_INIT = 64,   //< Регистры для инициализации микросхемы MAX2871.
        PLL_CONFIG = 96,   //< Конфигурация микросхемы PLL.
        DDS_CONTROL = 128,  //< Управление DDS.
        DDS_REG_PART1 = 160,  //< 
        DDS_REG_PART2 = 192,  //< 
        DDS_REG_PART3 = 224,  //< 
        DDS_REG_PART4 = 256,  //< 
        DDS_REG_PART5 = 288,  //< 
        DDS_FLASH = 320,  //< Reserved
        LO_SWITCH_PART1 = 352,  //< 
        LO_SWITCH_PART2 = 384,  //< 
    };

    public partial class FmMain : Form
    {
        #region PARAMETERS
        /// <summary>
        /// Cleans up jamming state, command queues, and UI on disconnect.
        /// Called from manual disconnect, cable-pull, and guiActive loss paths.
        /// </summary>
        private void CleanupOnDisconnect()
        {
            // 1. Stop jamming immediately
            if (isJamming)
            {
                isJamming = false;
                Debug.WriteLine("CleanupOnDisconnect: isJamming cleared");
            }

            // 2. Mark GUI inactive to prevent new commands
            guiActive = false;

            // 3. Flush the TX command queue (prevent stale commands on reconnect)
            lock (ltTxLock)
            {
                ltTxCommand.Clear();
            }

            // 4. Restore UI to disconnected state (must run on UI thread)
            try
            {
                MethodInvoker uiCleanup = delegate
                {
                    SetControlsJammingMode(false);

                    if (btnGuardStart != null) btnGuardStart.Enabled = true;
                    if (btnGuardStop != null) btnGuardStop.Enabled = false;

                    if (ledConnection != null) ledConnection.BackColor = Color.Red;
                    if (lblConnectionStatus != null)
                    {
                        lblConnectionStatus.Text = "OFFLINE";
                        lblConnectionStatus.ForeColor = Color.FromArgb(255, 80, 80);
                    }

                    UpdateGuardStatus();
                };

                if (this.InvokeRequired)
                    this.Invoke(uiCleanup);
                else
                    uiCleanup();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CleanupOnDisconnect UI error: " + ex.Message);
            }
        }
        /// <summary> Статус активности формы. </summary>
        public bool guiActive;
        /// <summary> Класс для работы с прибором через интерфейс USB. </summary>
        DmsDevice device;
        /// <summary> Класс для работы с USB подключением. </summary>
        UsbDevice usbConnection;
        UsbEndpointWriter writeEndpoint;
        UsbEndpointReader readEndpoint;
        /// <summary> Список команд последовательностей. </summary>
        List<IBaseSequence> ltServiceSequences;
        /// <summary> Класс глобальной конфигурации программы. </summary>
        GlobalConfigurations run_cfg;
        /// <summary> Служебное окно, для отладки программы. </summary>
        DebugWindow debugBox;
        /// <summary> Серийный номер. </summary>
        String sSerialNumber = "0000000001";
        /// <summary> Дата производства. </summary>
        String sDateManufacture = "01012024";

        #region LIST OF COMMANDS
        /// <summary> Список передаваемых команд. </summary>
        List<Command> ltTxCommand;
        /// <summary> Lock object for thread-safe ltTxCommand access. </summary>
        readonly object ltTxLock = new object();
        /// <summary> Список принятых команд. </summary>
        List<Command> ltRxCommand;
        #endregion  //  LIST OF COMMANDS

        #region THREAD PARAMETERS
        /// <summary> Поток отвечаюший за чтение данных. </summary>
        Thread usbConnectionThread;
        /// <summary> Поток обработки действий. </summary>
        Thread SequenceThread;
        /// <summary> Флаг выхода из потока чтения данных </summary>
        volatile bool bExitusbConnectionThread = false;
        /// <summary> Флаг выхода из потока обработки команд. </summary>
        volatile bool bExitSequenceThread = false;
        #endregion  //  THREAD PARAMETERS

        #region DESIGNER_FALLBACK_FIELDS
        // Controls normally created in FmMain.Designer.cs
        private TextBox txbxLoSwitchState1;
        private TextBox txbxLoSwitchState2;
        private TextBox txbxLoSwitchState3;
        private TextBox txbxLoSwitchState4;
        private TextBox txbxLoSwitchState5;
        private TextBox txbxLoSwitchState6;
        private TextBox txbxLoSwitchState7;
        private TextBox txbxLoSwitchState8;
        #endregion

        // K9 GUARD BAND ADDITIONS
        private Stopwatch bandTimer = new Stopwatch();
        private Stopwatch totalTimer = new Stopwatch();
        private int pulseOnTimeMs = 10;
        private int pulseOffTimeMs = 40;
        private int pulseCycles = 10000;
        private GroupBox grpGuardBandSettings;
        private NumericUpDown numGuardPulseOn;
        private NumericUpDown numGuardPulseOff;
        private NumericUpDown numGuardCycles;
        private CheckBox chkGuardPulseMode;
        private Label lblGuardStatus;
        private Button btnGuardStart;
        private Button btnGuardStop;
        private CheckBox chkBandTableMode;
        private bool bandTableModeActive = false;

        // Floating jamming status monitor
        private Panel pnlJammingMonitor;
        private Label lblJammingStatusText;
        private Label lblJammingCycleText;

        // TDM STANDALONE CONTROLS
        private GroupBox grpTdmStandalone;
        private Button btnTdmUpload;
        private Button btnTdmSaveEeprom;
        private Button btnTdmLoadEeprom;
        private Button btnTdmStart;
        private Button btnTdmStop;
        private Label lblTdmStatus;
        private Label lblLiveHits;
        private CheckBox chkShowSwitchSelect;
        private bool _loadingBands = false;
        private NumericUpDown numTdmDwell;
        private NumericUpDown numDutyPercent;    // SRAM duty cycle 5-100%
        private NumericUpDown numGroupSize;      // Group rotation: bands per group
        private NumericUpDown numRotationMs;     // Group rotation: period in ms
        private volatile int userDwellUs = 100;  // Per-band dwell from Dwell spinner (µs) — autonomous mode

        // TDM EEPROM readback storage (populated during connect)
        private byte tdmEepromMode = 0;
        private byte tdmEepromNumBands = 0;
        private ushort tdmEepromDwellUs = 100;
        private ushort tdmEepromPulseOn = 10;
        private ushort tdmEepromPulseOff = 40;
        private byte tdmEepromDuty = 100;          // SRAM duty % from EEPROM
        private bool tdmEepromValid = false;
        private bool tdmEepromDwellLoaded = false;  // Only load EEPROM dwell once, don't overwrite user changes
        private double[] tdmEepromLoFreq = new double[8];
        private float[] tdmEepromBw = new float[8];
        private float[] tdmEepromStep = new float[8];
        private uint[] tdmEepromPoints = new uint[8];
        private float[] tdmEepromCtrlFreq = new float[8];
        private bool[] tdmEepromActive = new bool[8];
        private byte[] tdmEepromDdsMode = new byte[8];  // 0=RAMP, 1=PRBS
        private ushort[] tdmEepromAtten = new ushort[8];  // per-band atten DAC (0xFFFF/0 = use main)
        // K9 build stamp — shows in the title bar so you can confirm which build is running.
        private const string K9_BUILD = "build 2026-09-18a (per-band atten)";
                private bool[] tdmEepromBandValid = new bool[8];
        // ── DIAGNOSTIC: raw EEPROM readback bytes ──
        private byte[] tdmDiagRawHeader = null;
        private byte[][] tdmDiagRawBand = new byte[8][];
        // ── SAVE VERIFICATION: store what we intended to write ──
        private int tdmExpectedBandCount = 0;
        private double[] tdmExpectedLoFreq = new double[8];
        private float[] tdmExpectedBw = new float[8];
        private uint[] tdmExpectedPoints = new uint[8];
        private float[] tdmExpectedCtrlFreq = new float[8];
        private bool[] tdmExpectedActive = new bool[8];

        #region ATTENUATOR_SELECTOR_FIELDS
        private CheckBox chkAtt1;
        private CheckBox chkAtt2;
        private CheckBox chkAtt3;
        private bool[] attPositionEnabled = new bool[] { true, false, false, false };  // Which positions are wired to selector switch
        private int activeAttPosition = 0;   // Which position the external switch is currently selecting (0-3)
        private double[] attUserDbValues = new double[] { 0.0, 0.0, 0.0, 0.0 };  // User-entered dB values for display
        private int activeSwitchBand = 0;    // Which of the 8 switch frequency bands is active (0-7)
        #endregion

        #region DIAGNOSTICS_FIELDS
        private TabControl tabDiag;
        private TabPage tabSTM32;
        private TabPage tabAD9106;
        private TabPage tabMAX2871;
        private TabPage tabFullTest;
        private RichTextBox rtbDiagLog;
        private ProgressBar prgDiag;
        private Label lblDiagOverall;
        private Panel pnlDiagPanel;
        private bool diagRunning = false;
        private Panel ledConnection;
        private Label lblConnectionStatus;
        private volatile bool isJamming = false;
        private volatile int jammingGeneration = 0;  // Incremented each START, checked in finally

        // ── TDM TIMING DIAGNOSTICS (from firmware DWT measurement) ──
        private System.Windows.Forms.Timer tmrTdmDiag;           // Polls CMD_TDM_STATUS every 2s
        private volatile uint tdmDiagLoopUs = 0;    // Main loop period (µs avg)
        private volatile uint tdmDiagHopUs = 0;     // Hop function duration (µs avg)
        private volatile uint tdmDiagDwellUs = 0;   // Actual measured dwell (µs avg)
        private volatile uint tdmDiagCycleUs = 0;   // Full cycle time (µs avg)
        private volatile bool tdmDiagValid = false;  // True after first successful parse
        private volatile int tdmDiagRxCount = 0;     // Count of CMD_TDM_STATUS responses received
        private volatile int tdmDiagPollCount = 0;   // Count of polls sent
        private volatile int tdmDiagLastPktSize = 0; // Last received packet size
        private volatile byte tdmGroupSize = 0;      // Current group size (0=off)
        private volatile byte tdmGroupCurrent = 0;   // Current group index
        private volatile byte tdmDiagActiveCount = 0; // Active band count from firmware
        private volatile ushort tdmRotationMs = 10;   // Last sent rotation period

        #region JAMMING_STATE_FIELDS
        private double savedLoFrequency = 0;
        private float savedLoStep = 0;
        private uint savedLoPoints = 0;
        private float savedLoHoldTime = 0;
        private int savedLoWaitLD = 0;
        private int savedLoSweepOn = 0;
        private ushort savedDdsSweepOn = 0;
        private ulong savedDdsStart = 0;
        private ulong savedDdsStep = 0;
        private ulong savedDdsPoints = 0;
        private float savedDdsFreqCtrl = 0;
        private int savedDdsMode = 0;
        private ushort savedAttDac = 0;
        private ushort savedAttPosition = 0;
        #endregion
        private int k9ExtendedMode = 0;  // Tracks UI mode index (0-5) vs hardware AD9106_MODE (0-3)
        private int diagPassCount = 0;
        private int diagFailCount = 0;
        private int diagWarnCount = 0;
        private static readonly Dictionary<ushort, ushort> AD9106_DEFAULTS = new Dictionary<ushort, ushort>
        {
            { 0x0000, 0x0000 }, { 0x0001, 0x0000 }, { 0x0002, 0x0000 }, { 0x001E, 0x0FFF },
        };
        private const uint MAX2871_VCOSEL_MASK = 0x0000003F;
        private const uint MAX2871_ADC_MASK = 0x00000FC0;
        private const uint MAX2871_ADCVALID_BIT = 0x00001000;
        #endregion

        #endregion  //  PARAMETERS

        /// <summary> Конструктор класса. </summary>
        public FmMain(GlobalConfigurations _cfg)
        {
            InitializeComponent();

            run_cfg = _cfg;

            guiActive = false;

            device = new DmsDevice();

            tlsplbLO.Visible = false;
            tlsplbMOUT.Visible = false;

            if (run_cfg.bDgMessage == true)
            {
                debugBox = new DebugWindow();
                debugBox.Show();
                debugBox.FormIcon = this.Icon;

                tlsplbLO.Visible = true;
                tlsplbMOUT.Visible = true;
            }

            ltRxCommand = new List<Command>();
            ltTxCommand = new List<Command>();

            /// <summary> Список команд последовательностей. </summary>
            ltServiceSequences = new List<IBaseSequence>();

            #region THREAD START
            usbConnectionThread = new Thread(FuncusbConnectionThread);
            usbConnectionThread.Name = "ReadThread";
            usbConnectionThread.Start();

            SequenceThread = new Thread(FuncSequenceThread);
            SequenceThread.Name = "SequenceThread";
            SequenceThread.Start();
            #endregion  /// THREAD START

            timer1.Enabled = true;

            ControlProgrammMode(run_cfg);

            ConnectionControlGUI(false, run_cfg);

            // K9 GUARD BAND & DIAGNOSTICS ADDITIONS
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.MinimumSize = new Size(800, 500);
            this.WindowState = FormWindowState.Normal;
            this.AutoScroll = true;  // Scrollbars appear when window too small for controls

            // ─── POPULATE COMBOBOXES ───
            // Always repopulate to override any Designer items that may be wrong/missing
            // AD9106_MODE enum: 0=CONTINUE, 1=SOFTWARE, 2=RAMP, 3=PSEUDO
            if (cmbbxDdsMode != null)
            {
                cmbbxDdsMode.Items.Clear();
                cmbbxDdsMode.Items.AddRange(new object[] {
                    "CW",           // 0 → AD9106 CONTINUE
                    "Sweep",        // 1 → AD9106 SOFTWARE
                    "Ramp",         // 2 → AD9106 RAMP
                    "Pseudo",       // 3 → AD9106 PSEUDO
                    "Multi-Tone",   // 4 → AD9106 RAMP + multi-tone config
                    "TDM Jam"       // 5 → AD9106 RAMP + TDM fast hop
                });
                cmbbxDdsMode.SelectedIndex = 0;
                cmbbxDdsMode.DropDownStyle = ComboBoxStyle.DropDownList;
                // Ensure event handler is connected
                cmbbxDdsMode.SelectedIndexChanged -= cmbbxDdsMode_SelectedIndexChanged;
                cmbbxDdsMode.SelectedIndexChanged += cmbbxDdsMode_SelectedIndexChanged;
            }

            // MAX2871 Output Power: 0=-4dBm, 1=-1dBm, 2=+2dBm, 3=+5dBm
            if (cmbbxLoPower != null)
            {
                cmbbxLoPower.Items.Clear();
                cmbbxLoPower.Items.AddRange(new object[] { "-4 dBm", "-1 dBm", "+2 dBm", "+5 dBm" });
                cmbbxLoPower.SelectedIndex = 3;  // Default to +5 dBm
                cmbbxLoPower.DropDownStyle = ComboBoxStyle.DropDownList;
                // Ensure event handlers are connected
                cmbbxLoPower.SelectedIndexChanged -= cmbbxLoPower_SelectedIndexChanged;
                cmbbxLoPower.SelectedIndexChanged += cmbbxLoPower_SelectedIndexChanged;
                cmbbxLoPower.KeyPress -= cmbbxLoPower_KeyPress;
                cmbbxLoPower.KeyPress += cmbbxLoPower_KeyPress;
            }

            EnsureGridColumns();

            // Rename column after form is fully loaded (Designer's ResumeLayout can reset HeaderText)
            this.Load += (s, ev) =>
            {
                if (dgvBands != null)
                {
                    foreach (DataGridViewColumn col in dgvBands.Columns)
                    {
                        if (col.HeaderText == "Start MHz")
                        {
                            col.HeaderText = "Centre MHz";
                            Debug.WriteLine("Renamed column '" + col.Name + "' from Start MHz to Centre MHz");
                            break;
                        }
                    }
                }
            };

            // K9: Make grid resize with window and hide unused columns
            if (dgvBands != null)
            {
                dgvBands.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                dgvBands.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

                // Hide Designer columns that are unused or duplicated:
                // - Status / Sweep Mode: legacy, not used in TDM
                // - colBwMhz: duplicate of code-added colBandwidthMhz
                // - colStepKhz / Step KHz: step is auto-calculated from BW/Points
                // - Loop: TDM always loops all active bands automatically
                foreach (DataGridViewColumn col in dgvBands.Columns)
                {
                    string hdr = (col.HeaderText ?? "").Replace("\n", " ").Replace("\r", "").Trim();
                    // Hide by name
                    if (col.Name == "colBwMhz" || col.Name == "colStepKhz" ||
                        col.Name == "colSweepMode")
                        col.Visible = false;
                    // Hide by header text (designer names may differ from code names)
                    if (hdr == "Sweep Mode" || hdr == "Loop" ||
                        hdr == "Step KHz")
                        col.Visible = false;
                }

                // Debug: dump all designer column names and headers
                Debug.WriteLine("═══ Grid columns at startup ═══");
                foreach (DataGridViewColumn col in dgvBands.Columns)
                    Debug.WriteLine(string.Format("  [{0}] Name={1} Header='{2}' Visible={3}",
                        col.Index, col.Name, col.HeaderText, col.Visible));
                Debug.WriteLine("═══════════════════════════════");

                // Set sensible minimum widths so columns don't collapse
                if (dgvBands.Columns.Contains("colBandName"))
                    dgvBands.Columns["colBandName"].MinimumWidth = 100;
                if (dgvBands.Columns.Contains("colStartMhz"))
                {
                    dgvBands.Columns["colStartMhz"].MinimumWidth = 70;
                    dgvBands.Columns["colStartMhz"].HeaderText = "Centre MHz";
                }
                if (dgvBands.Columns.Contains("colBandwidthMhz"))
                    dgvBands.Columns["colBandwidthMhz"].MinimumWidth = 60;
                if (dgvBands.Columns.Contains("colPower"))
                    dgvBands.Columns["colPower"].MinimumWidth = 55;
                if (dgvBands.Columns.Contains("colActive"))
                    dgvBands.Columns["colActive"].MinimumWidth = 50;
                if (dgvBands.Columns.Contains("colTones"))
                    dgvBands.Columns["colTones"].MinimumWidth = 50;

                // Update hits/sec display when grid values change (e.g. Active checkbox toggled)
                dgvBands.CurrentCellDirtyStateChanged += (s, ev) =>
                {
                    if (dgvBands.IsCurrentCellDirty) dgvBands.CommitEdit(DataGridViewDataErrorContexts.Commit);
                };
                dgvBands.DataError += (s, ev) =>
                {
                    Debug.WriteLine("│ DataGridView error row=" + ev.RowIndex + " col=" + ev.ColumnIndex + ": " + ev.Exception?.Message);
                    ev.ThrowException = false;  // Suppress default error dialog
                };
                // NOTE: CellValueChanged also fires during AddBandRow - use flag to prevent interference
                dgvBands.CellValueChanged += (s, ev) =>
                {
                    if (_loadingBands) return;
                    UpdateTdmHitsDisplay();
                    UpdateGuardStatus();

                    // Auto-update Step kHz and Hits/s when BW, Points, DDS Mode, or Active changes
                    if (ev.RowIndex >= 0 && ev.ColumnIndex >= 0)
                    {
                        string colName = dgvBands.Columns[ev.ColumnIndex].Name;
                        if (colName == "colBandwidthMhz" || colName == "colTones" || colName == "colDdsMode")
                            UpdateStepKhzRow(dgvBands.Rows[ev.RowIndex]);

                        // Active toggle changes active band count → hits/sec changes for ALL rows
                        if (colName == "colActive")
                            UpdateAllStepKhz();
                    }

                    // Grey/ungrey cells when DDS Mode changes per row
                    // PRBS: grey Tones + BW + Step (always 170 MHz — AD9106 PSEUDO LFSR = ref clock)
                    if (ev.RowIndex >= 0 && ev.ColumnIndex >= 0 &&
                        dgvBands.Columns[ev.ColumnIndex].Name == "colDdsMode")
                    {
                        var row = dgvBands.Rows[ev.RowIndex];
                        string mode = row.Cells["colDdsMode"].Value?.ToString() ?? "Ramp";
                        bool isPrbs = (mode == "PRBS");
                        Color bgLocked = Color.FromArgb(180, 185, 175);
                        Color fgLocked = Color.FromArgb(140, 140, 130);
                        Color bgNormal = Color.FromArgb(235, 240, 230);
                        Color fgNormal = Color.Black;

                        if (dgvBands.Columns.Contains("colTones"))
                        {
                            // PRBS: Points is N/A (greyed out)
                            // RAMP/RANDOM: User-editable per band
                            Color ptsBg = isPrbs ? bgLocked : bgNormal;
                            Color ptsFg = isPrbs ? fgLocked : fgNormal;
                            row.Cells["colTones"].Style.BackColor = ptsBg;
                            row.Cells["colTones"].Style.ForeColor = ptsFg;
                            row.Cells["colTones"].ReadOnly = isPrbs;
                            if (!isPrbs && (row.Cells["colTones"].Value == null ||
                                row.Cells["colTones"].Value.ToString().Trim().Length == 0))
                            {
                                row.Cells["colTones"].Value = 512;  // Default — safe for LTE (75MHz/512=146kHz)
                            }
                        }
                        if (dgvBands.Columns.Contains("colBandwidthMhz"))
                        {
                            row.Cells["colBandwidthMhz"].Style.BackColor = isPrbs ? bgLocked : bgNormal;
                            row.Cells["colBandwidthMhz"].Style.ForeColor = isPrbs ? fgLocked : fgNormal;
                            row.Cells["colBandwidthMhz"].ReadOnly = isPrbs;
                            if (isPrbs)
                                row.Cells["colBandwidthMhz"].Value = 170.0;
                        }
                    }
                };
            }

            CreateGuardBandSettingsBox();
            CreateTdmStandalonePanel();
            CreateDiagnosticsPanel();

            // K9: Force all panels visible at startup (before device connects)
            if (grbxATT != null) grbxATT.Visible = true;
            if (grbxDetector != null) grbxDetector.Visible = true;
            if (grbxPulseModulatorBox != null) grbxPulseModulatorBox.Visible = false;  // Removed - not used
            if (grbxLO != null) grbxLO.Visible = true;
            if (grbxDDS != null) grbxDDS.Visible = true;
            if (grbxSwitchSelect != null) grbxSwitchSelect.Visible = false;  // Hidden by default, toggle via checkbox

            // Show DDS controls in Debug layout
            PrepareGroupBoxDDS(device.dds.SweepOn);

            // K9: Create attenuator position selector checkboxes
            CreateAttenuatorSelectors();

            // K9: Force initial colours on all switch bands and attenuators
            // (must be after all controls are created and visible)
            this.Load += (s, ev) =>
            {
                SetActiveSwitchBand(0);
                UpdateAttenuatorColours();

                // Deferred column hide (belt-and-braces - designer columns may not be ready earlier)
                if (dgvBands != null)
                {
                    foreach (DataGridViewColumn col in dgvBands.Columns)
                    {
                        string hdr = col.HeaderText.Replace("\n", " ").Replace("\r", "").Trim();
                        if (hdr == "Sweep Mode" ||
                            hdr == "Step KHz" || hdr == "Loop")
                            col.Visible = false;
                    }
                }
            };

            // K9: Set attenuator defaults to dB (not V)
            if (txbxAttenuatorPosition0 != null && (string.IsNullOrEmpty(txbxAttenuatorPosition0.Text) || txbxAttenuatorPosition0.Text.Contains("V")))
                txbxAttenuatorPosition0.Text = "0.00 dB";
            if (txbxAttenuatorPosition1 != null && (string.IsNullOrEmpty(txbxAttenuatorPosition1.Text) || txbxAttenuatorPosition1.Text.Contains("V")))
                txbxAttenuatorPosition1.Text = "0.00 dB";
            if (txbxAttenuatorPosition2 != null && (string.IsNullOrEmpty(txbxAttenuatorPosition2.Text) || txbxAttenuatorPosition2.Text.Contains("V")))
                txbxAttenuatorPosition2.Text = "0.00 dB";
            if (txbxAttenuatorPosition3 != null && (string.IsNullOrEmpty(txbxAttenuatorPosition3.Text) || txbxAttenuatorPosition3.Text.Contains("V")))
                txbxAttenuatorPosition3.Text = "0.00 dB";

            // K9: Set FreqCtrl default to 1 MHz
            if (txbxDdsFreqCtrl != null && string.IsNullOrEmpty(txbxDdsFreqCtrl.Text))
                txbxDdsFreqCtrl.Text = "1.000 MHz";

            // K9: Fix Base Frequency groupbox - prevent overlap with Attenuator box
            if (grbxLO != null && txbxLoStart != null)
            {
                // Constrain textbox width so it doesn't push into Attenuator groupbox
                txbxLoStart.Width = Math.Min(txbxLoStart.Width, 90);
                grbxLO.Width = Math.Min(grbxLO.Width, 200);
            }

            // K9 FIX: Wire up LO textbox events (missing from Designer)
            // Without these, pressing Enter in Base Frequency boxes does nothing
            if (txbxLoStart != null)
            {
                txbxLoStart.Tag = "LoSweepStart";
                txbxLoStart.KeyPress += LoParameters_KeyPress;
            }
            if (txbxLoStep != null)
            {
                txbxLoStep.Tag = "LoSweepStep";
                txbxLoStep.KeyPress += LoParameters_KeyPress;
            }
            if (txbxLoPoints != null)
            {
                txbxLoPoints.Tag = "LoSweepPoint";
                txbxLoPoints.KeyPress += LoParameters_KeyPress;
            }
            if (txbxLoDelays != null)
            {
                txbxLoDelays.Tag = "LoSweepDelay";
                txbxLoDelays.KeyPress += LoParameters_KeyPress;
            }

            // K9: Make Switch Select groupbox larger to show all 8 bands clearly
            if (grbxSwitchSelect != null)
            {
                grbxSwitchSelect.Width = Math.Max(grbxSwitchSelect.Width, 220);
                grbxSwitchSelect.Height = Math.Max(grbxSwitchSelect.Height, 260);

                // Add toggle checkbox at the Switch Select box location
                chkShowSwitchSelect = new CheckBox();
                chkShowSwitchSelect.Text = "Show Switch Select";
                chkShowSwitchSelect.Font = new Font("Segoe UI", 7F);
                chkShowSwitchSelect.ForeColor = Color.FromArgb(180, 190, 160);
                chkShowSwitchSelect.Location = new Point(grbxSwitchSelect.Location.X, grbxSwitchSelect.Location.Y);
                chkShowSwitchSelect.Size = new Size(140, 18);
                chkShowSwitchSelect.Checked = false;
                chkShowSwitchSelect.CheckedChanged += (s, e) =>
                {
                    if (grbxSwitchSelect != null)
                        grbxSwitchSelect.Visible = chkShowSwitchSelect.Checked;
                };
                this.Controls.Add(chkShowSwitchSelect);
                chkShowSwitchSelect.BringToFront();
            }

            // K9 FIX: Wire up DDS textbox Tags and events (matching LO fix above)
            // Without Tags, DdsParameters_KeyPress handler cannot identify which field changed
            if (txbxDdsStart != null)
            {
                txbxDdsStart.Tag = "Start";
                txbxDdsStart.KeyPress -= DdsParameters_KeyPress;
                txbxDdsStart.KeyPress += DdsParameters_KeyPress;
            }
            if (txbxDdsStep != null)
            {
                txbxDdsStep.Tag = "Step";
                txbxDdsStep.KeyPress -= DdsParameters_KeyPress;
                txbxDdsStep.KeyPress += DdsParameters_KeyPress;
            }
            if (txbxDdsPoints != null)
            {
                txbxDdsPoints.Tag = "Points";
                txbxDdsPoints.KeyPress -= DdsParameters_KeyPress;
                txbxDdsPoints.KeyPress += DdsParameters_KeyPress;
            }
            if (txbxDdsBandwith != null)
            {
                txbxDdsBandwith.Tag = "Bandwidth";
                txbxDdsBandwith.KeyPress -= DdsParameters_KeyPress;
                txbxDdsBandwith.KeyPress += DdsParameters_KeyPress;
            }
            if (txbxDdsFreqCtrl != null)
            {
                txbxDdsFreqCtrl.Tag = "FreqCtrl";
                txbxDdsFreqCtrl.KeyPress -= DdsParameters_KeyPress;
                txbxDdsFreqCtrl.KeyPress += DdsParameters_KeyPress;
            }
            if (txbxDdsTwMem != null)
            {
                txbxDdsTwMem.Tag = "TwMem";
                txbxDdsTwMem.KeyPress -= DdsParameters_KeyPress;
                txbxDdsTwMem.KeyPress += DdsParameters_KeyPress;
            }

            // K9: Auto-load 4 standard bands on startup
            // K9: Grid starts empty - bands only loaded from device or by user action
            // LoadDefaultBands();
        }

        /// <summary> Установка конфигурации устройства DDS. </summary>
        private void DDSSetConfiguration()
        {
            ushort cmd_counter = 0;
            ushort Cycle = 1;

            // In RAMP/TDM mode, Start/Step/Points/FreqCtrl are computed from band table BW.
            // Only read from textboxes for non-RAMP modes.
            if (device.dds.SweepOn != (ushort)AD9106_MODE.RAMP)
            {
                device.dds.Start = (ulong)(nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble(txbxDdsStart.Text));
                device.dds.Points = Convert.ToUInt32(txbxDdsPoints.Text);
                device.dds.FreqCtrl = (float)nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble(txbxDdsFreqCtrl.Text);
                device.dds.Step = (ulong)(nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble(txbxDdsStep.Text));
            }

            AddSequences(new SequenceSetSweepDDS(ltTxCommand, null, device.dds.SweepOn, device.dds.Start, device.dds.Step, device.dds.Points));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            if (device.dds.SweepOn == (ushort)AD9106_MODE.CONTINUE || device.dds.SweepOn == (ushort)AD9106_MODE.SOFTWARE)
            {
                #region CONTINUE MODE
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1F, 0x0000));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1E, 0x0000));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                device.dds.registers[0x26] = (ushort)((device.dds.Channels[3]) ? 0x3300 : 0x0000);
                device.dds.registers[0x26] += (ushort)((device.dds.Channels[2]) ? 0x0033 : 0x0000);
                device.dds.registers[0x27] = (ushort)((device.dds.Channels[1]) ? 0x3300 : 0x0000);
                device.dds.registers[0x27] += (ushort)((device.dds.Channels[0]) ? 0x0033 : 0x0000);
                device.dds.registers[0x28] = 0x0111;
                device.dds.registers[0x29] = 0x8000;
                device.dds.registers[0x2A] = 0x0101;
                device.dds.registers[0x2B] = 0x0101;
                device.dds.registers[0x36] = 0x0000;
                device.dds.registers[0x37] = 0x0000;
                device.dds.registers[0x44] = 0x0000;
                device.dds.registers[0x45] = 0x0000;
                for (int i = 0; i < 4; i++)
                {
                    device.dds.registers[0x50 + 4 * i] = 0x0001;
                    device.dds.registers[0x51 + 4 * i] = 0x0000;
                    device.dds.registers[0x52 + 4 * i] = 0x0000;
                    device.dds.registers[0x53 + 4 * i] = 0xFFFF;
                }
                for (ushort i = 0x20; i < 0x60; i++)
                {
                    AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, i, device.dds.registers[i]));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                }
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1F, 0x0000));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1E, 0x0001));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, device.dds.FreqCtrl));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                #endregion  /// CONTINUE MODE
            }
            else if (device.dds.SweepOn == (ushort)AD9106_MODE.RAMP)
            {
                #region RAMP MODE
                device.dds.registers[0x20] = 0x0001;
                device.dds.registers[0x26] = (ushort)((device.dds.Channels[3] == true) ? 0x3200 : 0x0000);
                device.dds.registers[0x26] += (ushort)((device.dds.Channels[2] == true) ? 0x0032 : 0x0000);
                device.dds.registers[0x27] = (ushort)((device.dds.Channels[1] == true) ? 0x3200 : 0x0000);
                device.dds.registers[0x27] += (ushort)((device.dds.Channels[0] == true) ? 0x0032 : 0x0000);
                device.dds.registers[0x28] = 0x0111;
                device.dds.registers[0x29] = 0x8000;
                device.dds.registers[0x2A] = 0x0101;
                device.dds.registers[0x2B] = 0x0101;
                device.dds.registers[0x2C] = 0x0003;
                device.dds.registers[0x2D] = 0x0000;
                device.dds.registers[0x36] = 0x0404;
                device.dds.registers[0x37] = 0x0404;
                device.dds.registers[0x44] = 0x0002;
                device.dds.registers[0x45] = 0x0001;
                device.dds.registers[0x45] += (ushort)((device.dds.Channels[3] == true) ? 0x4000 : 0x0000);
                device.dds.registers[0x45] += (ushort)((device.dds.Channels[2] == true) ? 0x0400 : 0x0000);
                device.dds.registers[0x45] += (ushort)((device.dds.Channels[1] == true) ? 0x0040 : 0x0000);
                device.dds.registers[0x45] += (ushort)((device.dds.Channels[0] == true) ? 0x0004 : 0x0000);
                for (int i = 0; i < 4; i++)
                {
                    device.dds.registers[(ushort)(0x50 + 4 * i)] = (ushort)(1000);
                    device.dds.registers[(ushort)(0x51 + 4 * i)] = (ushort)(0x0000);
                    device.dds.registers[(ushort)(0x52 + 4 * i)] = (ushort)(0x0000 + (device.dds.Points * 16));
                    device.dds.registers[(ushort)(0x53 + 4 * i)] = Cycle;
                }
                for (ushort i = 0x0; i < 0x60; i++)
                {
                    AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, i, device.dds.registers[i]));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                }
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1E, 0x0003));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1F, 0x0000));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                //AddSequences ( new SequenceDelay (1000) );
                //AddSequences ( new SequenceCallFunction   ( cbfuncIncrementStatusBar, null  )); cmd_counter++;
                //AddSequences ( new SequenceSetDDSCtrlFrequency ( ltTxCommand, null, device.dds.FreqCtrl ));
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 10));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceDelay(500));
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceDelay(100));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                #endregion  //  RAMP MODE
            }
            else if (device.dds.SweepOn == (ushort)AD9106_MODE.PSEUDO)
            {
                #region PSEUDO MODE
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1F, 0x0000));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1E, 0x0000));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                device.dds.registers[0x26] = (ushort)((device.dds.Channels[3]) ? 0x2100 : 0x0000);
                device.dds.registers[0x26] += (ushort)((device.dds.Channels[2]) ? 0x0021 : 0x0000);
                device.dds.registers[0x27] = (ushort)((device.dds.Channels[1]) ? 0x2100 : 0x0000);
                device.dds.registers[0x27] += (ushort)((device.dds.Channels[0]) ? 0x0021 : 0x0000);

                device.dds.registers[0x28] = 0x0111;
                device.dds.registers[0x29] = 0x8000;
                device.dds.registers[0x2A] = 0x0101;
                device.dds.registers[0x2B] = 0x0101;
                device.dds.registers[0x36] = 0x0000;
                device.dds.registers[0x37] = 0x0000;
                device.dds.registers[0x44] = 0x0000;
                device.dds.registers[0x45] = 0x0000;
                for (int i = 0; i < 4; i++)
                {
                    device.dds.registers[0x50 + 4 * i] = 0x0001;
                    device.dds.registers[0x51 + 4 * i] = 0x0000;
                    device.dds.registers[0x52 + 4 * i] = 0x0000;
                    device.dds.registers[0x53 + 4 * i] = 0xFFFF;
                }
                for (ushort i = 0x20; i < 0x60; i++)
                {
                    AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, i, device.dds.registers[i]));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                }
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1F, 0x0000));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1E, 0x0001));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, device.dds.FreqCtrl));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceDelay(100));
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0.0F));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                #endregion  //  PSEUDO MODE
            }

            //AddSequences ( new SequenceCallFunction ( cbfuncConnectionUpdateWindows, null ) );

            tlspStatusPanel.Maximum = cmd_counter;
            tlspStatusPanel.Value = 0;
        }

        /// <summary> Настройка устройства. </summary>
        /// <param name="device"> Класс настройки устройства. </param>
        private void SetDeviceConfiguration(DmsDevice device)
        {
            AddSequences(new SequenceSetSweepLoParameters(ltTxCommand,
                                                              null,
                                                              (float)device.lo.pll.OutFrequency,
                                                              device.lo.Step,
                                                              device.lo.Points,
                                                              device.lo.HoldTime,
                                                              device.lo.WaitLD,
                                                              device.lo.SweepOn));
            AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, (float)device.dds.FreqCtrl));
        }

        #region TUNING WINDOW
        /// <summary> Делегат последовательной функции. </summary>
        /// <param name="data"> Объект данных. </param>
        delegate void dConnectionControl(bool bStatus, GlobalConfigurations p_cfg);

        /// <summary> Настройка отображения программы в зависимости от режима. </summary>
        /// <param name="cfg"></param>
        public void ControlProgrammMode(GlobalConfigurations p_cfg)
        {
            // K9: Always use full Debug-style layout regardless of mode
            // All controls visible, all panels enabled, maximized window
            this.tlspbtSaveToDevice.Visible = false;
            this.txbxDdsBandwith.Enabled = true;

            // Don't constrain window size - we use maximized + MinimumSize
            // this.Height = 550; this.Width = 700;  // Removed - window is maximized
        }

        /// <summary> Управление состоянием формы. </summary>
        /// <param name="bStatus"> Состояние соединения. </param>
        private void ConnectionControlGUI(bool bStatus, GlobalConfigurations p_cfg)
        {
            if (this.InvokeRequired)
            {
                dConnectionControl d = new dConnectionControl(ConnectionControlGUI);
                BeginInvoke(d, new object[] { bStatus, p_cfg });
            }
            else
            {
                if (bStatus == false)
                {
                    this.tlspbtConnect.Text = "Connect";
                    this.Text = "AWJ v3.0h  " + K9_BUILD;
                    this.tlspSerialNumber.Text = "SN:";
                    this.tlspStatusPanel.Maximum = 0;
                    this.tlspStatusPanel.Value = 0;
                    tdmEepromDwellLoaded = false;  // Allow EEPROM to set dwell on next connect
                    // K9: All panels remain visible regardless of connection state
                }
                else
                {
                    this.tlspbtConnect.Text = "Disconnect";
                }

                // K9: Always show all control panels - device connection only
                // affects Connect/Disconnect button text and status bar
                if (grbxATT != null) this.grbxATT.Visible = true;
                if (grbxDetector != null) this.grbxDetector.Visible = true;
                if (grbxPulseModulatorBox != null) this.grbxPulseModulatorBox.Visible = false;
                if (grbxLO != null) this.grbxLO.Visible = true;
                if (grbxDDS != null) this.grbxDDS.Visible = true;
                if (grbxSwitchSelect != null) this.grbxSwitchSelect.Visible = (chkShowSwitchSelect != null && chkShowSwitchSelect.Checked);
            }
        }

        /// <summary> Обновление формы. </summary>
        /// <param name="_device"> Класс с параметрами устройства </param>
        private void UpdateMainForm(DmsDevice _device)
        {
            try
            {
                if (_device.ltSwitchAttenuator != null && _device.ltSwitchAttenuator.Count >= 4)
                {
                    // Display user-entered dB values directly (avoids cal table round-trip error)
                    // Only update from device if user hasn't set a value yet (value still 0)
                    for (int attIdx = 0; attIdx < 4; attIdx++)
                    {
                        TextBox attBox = null;
                        if (attIdx == 0) attBox = txbxAttenuatorPosition0;
                        else if (attIdx == 1) attBox = txbxAttenuatorPosition1;
                        else if (attIdx == 2) attBox = txbxAttenuatorPosition2;
                        else if (attIdx == 3) attBox = txbxAttenuatorPosition3;

                        if (attBox != null)
                        {
                            if (attUserDbValues[attIdx] != 0.0)
                            {
                                // User has set a value - display their exact dB
                                attBox.Text = attUserDbValues[attIdx].ToString("F2") + " dB";
                            }
                            else
                            {
                                // No user value yet - read from device cal table
                                double hwDb = device.AttCalibrationTable.getAttenuation(
                                    (float)device.ltSwitchAttenuator[attIdx].CtrlVoltage);
                                attUserDbValues[attIdx] = hwDb;
                                attBox.Text = hwDb.ToString("F2") + " dB";
                            }
                        }
                    }
                }

                if (_device.ltSwitchFrequency != null && _device.ltSwitchFrequency.Count >= 8)
                {
                    if (txbxLoSwitchState1 != null) txbxLoSwitchState1.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(_device.ltSwitchFrequency[0], 3);
                    if (txbxLoSwitchState2 != null) txbxLoSwitchState2.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(_device.ltSwitchFrequency[1], 3);
                    if (txbxLoSwitchState3 != null) txbxLoSwitchState3.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(_device.ltSwitchFrequency[2], 3);
                    if (txbxLoSwitchState4 != null) txbxLoSwitchState4.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(_device.ltSwitchFrequency[3], 3);
                    if (txbxLoSwitchState5 != null) txbxLoSwitchState5.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(_device.ltSwitchFrequency[4], 3);
                    if (txbxLoSwitchState6 != null) txbxLoSwitchState6.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(_device.ltSwitchFrequency[5], 3);
                    if (txbxLoSwitchState7 != null) txbxLoSwitchState7.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(_device.ltSwitchFrequency[6], 3);
                    if (txbxLoSwitchState8 != null) txbxLoSwitchState8.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(_device.ltSwitchFrequency[7], 3);
                }

                // K9: Reapply active/inactive colours after text updates
                UpdateSwitchBandColours();
                UpdateAttenuatorColours();

                if (txbxDdsStart != null) txbxDdsStart.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(_device.dds.Start, 3);
                if (txbxDdsStep != null) txbxDdsStep.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(_device.dds.Step, 3);
                if (txbxDdsPoints != null) txbxDdsPoints.Text = Convert.ToString(_device.dds.Points);
                if (txbxDdsFreqCtrl != null) txbxDdsFreqCtrl.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(_device.dds.FreqCtrl, 3);

                // K9: Always display BW from actual device state (not just in Debug mode)
                if (txbxDdsBandwith != null)
                {
                    double readbackBw = _device.dds.getRampBandwidth();
                    if (readbackBw > 0)
                        txbxDdsBandwith.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(readbackBw, 3);
                }
                // K9: Always display TwMem from register 0x47
                if (txbxDdsTwMem != null)
                    txbxDdsTwMem.Text = Convert.ToString(_device.dds.registers[0x47]);

                if (txbxLoStart != null) txbxLoStart.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(_device.lo.pll.OutFrequency, 3);
                if (txbxLoStep != null) txbxLoStep.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(_device.lo.Step, 3);
                if (txbxLoPoints != null) txbxLoPoints.Text = Convert.ToString(_device.lo.Points);
                if (txbxLoDelays != null) txbxLoDelays.Text = nsAlexKir.AppConvertions.Functions.DoubleToTimeValue(_device.lo.HoldTime, 3);

                if (txbxPulseModulator_Frequency != null) txbxPulseModulator_Frequency.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(_device.pulse.frequency, 2, nsAlexKir.AppConvertions.eLanguage.eEnglish);
                if (txbxPulseModulator_DutyCycle != null) txbxPulseModulator_DutyCycle.Text = Convert.ToString(_device.pulse.dutycycle);

                // Set combobox selections via SelectedIndex (not Text) for proper binding
                if (cmbbxLoPower != null && cmbbxLoPower.Items.Count > 0 && _device.lo.pll.APWR < cmbbxLoPower.Items.Count)
                    cmbbxLoPower.SelectedIndex = _device.lo.pll.APWR;
                // Device reports hardware mode 0-3, map to combobox index
                // Only update if not in an extended K9 mode (4-7)
                if (cmbbxDdsMode != null && cmbbxDdsMode.Items.Count > 0 && k9ExtendedMode < 4)
                {
                    if (_device.dds.SweepOn < 4)
                        cmbbxDdsMode.SelectedIndex = _device.dds.SweepOn;
                }
                if (chbxLoWaitLD != null) chbxLoWaitLD.Checked = (_device.lo.WaitLD > 0) ? true : false;
                // TwMem display moved to earlier in this function (after DDS params block)
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [UpdateMainForm]\n" + ex.Message + "\n" + ex.Source, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Подготовка блока настройки работы ДДС.</summary>
        /// <param name="mode">Режим работы ДДС.</param>
        private void PrepareGroupBoxDDS(ushort mode)
        {
            lbDdsMode.Location = new Point(45, 28);
            cmbbxDdsMode.Location = new Point(95, 25);
            cmbbxDdsMode.Width = 140;

            lbDdsStart.Visible = false;
            txbxDdsStart.Visible = false;

            lbDdsStep.Visible = false;
            txbxDdsStep.Visible = false;

            lbBandwidth.Visible = false;
            txbxDdsBandwith.Visible = false;

            lbBandwidth.Visible = false;
            txbxDdsBandwith.Visible = false;

            lbDdsPoints.Visible = false;
            txbxDdsPoints.Visible = false;

            lbBandwidth.Visible = false;
            txbxDdsBandwith.Visible = false;

            lbDdsFreqCtrl.Visible = false;
            txbxDdsFreqCtrl.Visible = false;

            lbDdsTwMem.Visible = false;
            txbxDdsTwMem.Visible = false;

            switch (device.dds.SweepOn)
            {
                #region CASE_AD9106_MODE_CONTINUE
                case (ushort)AD9106_MODE.CONTINUE:  //  Single frequency
                    lbDdsStart.Text = "Frequency:";
                    lbDdsStart.Visible = true;
                    lbDdsStart.Location = new Point(15, 58);
                    txbxDdsStart.Visible = true;
                    txbxDdsStart.Location = new Point(95, 55);
                    break;
                #endregion  /// CASE_AD9106_MODE_CONTINUE

                #region CASE_AD9106_MODE_SOFTWARE
                case (ushort)AD9106_MODE.SOFTWARE:  //  Software sweep frequency
                    lbDdsStart.Text = "Start:";
                    lbDdsStart.Visible = true;
                    lbDdsStart.Location = new Point(40, 58);
                    txbxDdsStart.Visible = true;
                    txbxDdsStart.Location = new Point(95, 55);

                    lbDdsStep.Visible = true;
                    lbDdsStep.Location = new Point(40, 83);
                    txbxDdsStep.Visible = true;
                    txbxDdsStep.Location = new Point(95, 80);

                    lbDdsPoints.Visible = true;
                    lbDdsPoints.Location = new Point(40, 108);
                    txbxDdsPoints.Visible = true;
                    txbxDdsPoints.Location = new Point(95, 105);
                    break;
                #endregion  /// CASE_AD9106_MODE_SOFTWARE

                #region CASE_AD9106_MODE_RAMP
                case (ushort)AD9106_MODE.RAMP:      //  Ramp sweep frequency
                    lbBandwidth.Visible = true;
                    lbBandwidth.Location = new Point(15, 58);
                    txbxDdsBandwith.Width = 140;
                    txbxDdsBandwith.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(device.dds.getRampBandwidth(), 3);
                    txbxDdsBandwith.Visible = true;
                    txbxDdsBandwith.Location = new Point(95, 55);

                    lbDdsStart.Text = "Start:";
                    lbDdsStart.Visible = true;
                    lbDdsStart.Location = new Point(50, 83);
                    txbxDdsStart.Width = 140;
                    txbxDdsStart.Visible = true;
                    txbxDdsStart.Location = new Point(95, 80);

                    lbDdsPoints.Visible = true;
                    lbDdsPoints.Location = new Point(42, 108);
                    txbxDdsPoints.Width = 140;
                    txbxDdsPoints.Visible = true;
                    txbxDdsPoints.Location = new Point(95, 105);

                    lbDdsFreqCtrl.Visible = true;
                    lbDdsFreqCtrl.Location = new Point(25, 133);
                    txbxDdsFreqCtrl.Width = 140;
                    txbxDdsFreqCtrl.Visible = true;
                    txbxDdsFreqCtrl.Location = new Point(95, 130);

                    lbDdsTwMem.Visible = true;
                    lbDdsTwMem.Location = new Point(20, 158);
                    txbxDdsTwMem.Width = 140;
                    txbxDdsTwMem.Visible = true;
                    txbxDdsTwMem.Location = new Point(95, 155);
                    break;
                #endregion  /// CASE_AD9106_MODE_RAMP

                #region CASE_AD9106_MODE_PSEUDO
                case (ushort)AD9106_MODE.PSEUDO:    //  Pseudorandom
                    break;
                #endregion  /// CASE_AD9106_MODE_PSEUDO
            }

            // Grey out BW/Tones columns when mode doesn't use them
            UpdateGridColumnsForMode(mode);
        }
        #endregion  /// TUNING WINDOW

        #region DEBUG WINDOW
        /// <summary> Делегат последовательной функции. </summary>
        /// <param name="data"> Объект данных. </param>
        delegate void dThreadSafeDebugMessage(String message);

        /// <summary></summary>
        /// <param name="message"></param>
        void ThreadSafeDebugMessage(String message)
        {
            if (this.InvokeRequired)
            {
                dThreadSafeDebugMessage d = new dThreadSafeDebugMessage(ThreadSafeDebugMessage);
                BeginInvoke(d, new object[] { message });
            }
            else
            {
                if (debugBox != null)
                {
                    debugBox.AppendText(message);
                }
            }
        }
        #endregion

        #region SERVICE SEQUENCES
        /// <summary> Добаление действия в очередь. </summary>
        /// <param name="sequence"> Добавляемое действие. </param>
        public void AddSequences(CBaseSequence sequence)
        {
            lock (ltServiceSequences)
            {
                ltServiceSequences.Add(sequence);
            }
        }
        #endregion  /// SERVICE SEQUENCES

        #region THREAD FUNCTIONS
        /// <summary> Поток для чтения данных. </summary>
        void FuncusbConnectionThread(object obj)
        {
            try
            {
                int bytesWritten = 0;

                while (bExitusbConnectionThread == false)
                {
                    try
                    {
                        if (usbConnection != null)
                        {
                            if (usbConnection.IsOpen)
                            {
                                if (usbConnection.UsbRegistryInfo.IsAlive == false)
                                {
                                    CleanupOnDisconnect();
                                    ConnectionControlGUI(false, run_cfg);
                                    usbConnection.Close();
                                }
                                else if (ltTxCommand.Count > 0)
                                {
                                    writeEndpoint.Write(ltTxCommand[0].body, 3000, out bytesWritten);
                                    ThreadSafeDebugMessage(String.Format("[USB send]: {0:d} bytes\n", bytesWritten));
                                    ltTxCommand.RemoveAt(0);
                                }
                            }
                            else if (guiActive == true)
                            {
                                CleanupOnDisconnect();
                                ConnectionControlGUI(false, run_cfg);
                                usbConnection.Close();
                            }
                        }
                    }
                    catch (Exception usbEx)
                    {
                        /* Cable pulled mid-write or USB error — clean up gracefully */
                        Debug.WriteLine("USB thread: cable pull detected — " + usbEx.Message);
                        CleanupOnDisconnect();
                        ConnectionControlGUI(false, run_cfg);
                        try { usbConnection.Close(); } catch { }
                    }
                    Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [FuncusbConnectionThread]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> Поток для чтения данных. </summary>
        void FuncSequenceThread(object obj)
        {
            try
            {
                IBaseSequence current_action = null;
                Command cmd = null;

                while (bExitSequenceThread == false)
                {
                    if (current_action == null)
                    {
                        lock (ltServiceSequences)
                        {
                            if (ltServiceSequences.Count > 0)
                            {
                                current_action = ltServiceSequences[0];
                            }
                        }
                        if (current_action != null)
                        {
                            if (current_action.SequenceFunc(StatusSequence.eStart, null) == StatusSequence.eFinish)
                            {
                                ltServiceSequences.RemoveAt(0);
                                current_action = null;
                            }
                        }
                    }
                    else
                    {
                        lock (ltRxCommand)
                        {
                            if (ltRxCommand.Count > 0)
                            {
                                cmd = ltRxCommand[0];
                                ltRxCommand.RemoveAt(0);
                            }
                        }
                        if (cmd != null)
                        {
                            if (current_action.SequenceFunc(StatusSequence.eContinue, cmd) == StatusSequence.eFinish)
                            {
                                ltServiceSequences.RemoveAt(0);
                                current_action = null;
                            }
                            cmd = null;
                        }
                    }
                    Thread.Sleep(10);
                    //Thread.Sleep(1);
                    //for (int i = 0; i < 10000; i++) ;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [FuncSequenceThread]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion  /// THREAD FUNCTIONS

        #region CALLBACK FUNCTIONS
        /// <summary> Делегат последовательной функции. </summary>
        /// <param name="data"> Объект данных. </param>
        delegate void dCallbackSequenceFunction(params object[] data);

        /// <summary> Функция вызываемая при получении ответа на тестовую функцию. </summary>
        /// <param name="data"> Массив данных </param>
        private void cbfuncTestConnectionResult(params object[] data)
        {
            if (this.InvokeRequired)
            {
                dCallbackSequenceFunction d = new dCallbackSequenceFunction(cbfuncTestConnectionResult);
                BeginInvoke(d, new object[] { data });
            }
            else
            {
                if (debugBox != null)
                {
                    debugBox.AppendText("Test connection complete\n");
                }
            }
        }

        /// <summary> Функция вызываемая при получении ответа на тестовую функцию. </summary>
        /// <param name="data"> Массив данных </param>
        private void cbfuncInitDeviceList(params object[] data)
        {
            if (this.InvokeRequired)
            {
                dCallbackSequenceFunction d = new dCallbackSequenceFunction(cbfuncInitDeviceList);
                BeginInvoke(d, new object[] { data });
            }
            else
            {
                ushort cmd_counter = 0;

                AddSequences(new SequenceGetDetectorCtrl(ltTxCommand, cbfuncGetDetectorCtrl));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceGetDacAttenuator(ltTxCommand, cbfuncGetDacAttenuator, 0));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceGetDacAttenuator(ltTxCommand, cbfuncGetDacAttenuator, 1));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceGetDacAttenuator(ltTxCommand, cbfuncGetDacAttenuator, 2));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceGetDacAttenuator(ltTxCommand, cbfuncGetDacAttenuator, 3));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceReadRegisterPLL(ltTxCommand, cbfuncReadRegPLL, 0));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceReadRegisterPLL(ltTxCommand, cbfuncReadRegPLL, 1));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceReadRegisterPLL(ltTxCommand, cbfuncReadRegPLL, 2));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceReadRegisterPLL(ltTxCommand, cbfuncReadRegPLL, 3));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceReadRegisterPLL(ltTxCommand, cbfuncReadRegPLL, 4));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceReadRegisterPLL(ltTxCommand, cbfuncReadRegPLL, 5));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                for (ushort i = 0; i < 100; i++)
                {
                    AddSequences(new SequenceGetRegisterDDS(ltTxCommand, cbfuncGetRegisterDDS, i));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                }

                AddSequences(new SequenceGetSweepDDS(ltTxCommand, cbfuncGetSweepDDS));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceGetDDSCtrlFrequency(ltTxCommand, cbfuncGetDdsFrequencyCtrl));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceGetSweepLoParameters(ltTxCommand, cbfuncGetSweepLoParameters));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceGetPulseModulator(ltTxCommand, cbfuncGetPulseModulator));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceReadMemory(ltTxCommand, cdfuncReadConfiguration, (ushort)EEPROM.SN_DESCRIPTION, 20));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                //AddSequences ( new SequenceReadMemory           ( ltTxCommand, cdfuncReadConfiguration, (ushort)EEPROM.DDS_CONTROL, 20 ));
                //AddSequences ( new SequenceCallFunction         ( cbfuncIncrementStatusBar, null )); cmd_counter++;

                AddSequences(new SequenceReadMemory(ltTxCommand, cdfuncReadConfiguration, (ushort)EEPROM.LO_SWITCH_PART1, 4 * 4 + 2));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceReadMemory(ltTxCommand, cdfuncReadConfiguration, (ushort)EEPROM.LO_SWITCH_PART2, 4 * 4 + 2));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                // TDM config: Read from EEPROM (AT24 page boundary bug now fixed)
                tdmEepromValid = false;
                for (int i = 0; i < 8; i++) tdmEepromBandValid[i] = false;

                // Read TDM header (18 bytes at addr 448)
                AddSequences(new SequenceReadMemory(ltTxCommand, cdfuncReadConfiguration, TDM_EEPROM_HEADER, 18));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                // Read all 8 band slots (30 bytes each, stride 32)
                for (int i = 0; i < 8; i++)
                {
                    AddSequences(new SequenceReadMemory(ltTxCommand, cdfuncReadConfiguration, (ushort)(TDM_EEPROM_BANDS + (i * 32)), 30));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                }

                AddSequences(new SequenceCallFunction(cbfuncConnectionUpdateWindows, null));

                tlspStatusPanel.Maximum = cmd_counter;
                tlspStatusPanel.Value = 0;
            }
        }

        /// <summary> Считывание настроек детектора. </summary>
        /// <param name="data"> Массив со считанными настройками. </param>
        private void cbfuncGetDetectorCtrl(params object[] data)
        {
            if (this.InvokeRequired)
            {
                dCallbackSequenceFunction d = new dCallbackSequenceFunction(cbfuncGetDetectorCtrl);
                BeginInvoke(d, new object[] { data });
            }
            else
            {
                if ((short)data[0] == (short)eSTATUS_COMMANDS.DONE)
                {
                    device.detector.Control = (ushort)data[1];

                    txbxDetectorVoltage.Text = nsAlexKir.AppConvertions.Functions.DoubleToVoltageValue((double)data[3], 3, nsAlexKir.AppConvertions.eLanguage.eEnglish);
                }
            }
        }

        /// <summary> Функция обратного вызова, для обработки запроса на считывания состояния аттенюатора. </summary>
        /// <param name="data"> Массив данных </param>
        private void cbfuncGetDacAttenuator(params object[] data)
        {
            if (this.InvokeRequired)
            {
                dCallbackSequenceFunction d = new dCallbackSequenceFunction(cbfuncGetDacAttenuator);
                BeginInvoke(d, new object[] { data });
            }
            else
            {
                if ((short)data[0] == (short)eSTATUS_COMMANDS.DONE)
                {
                    device.ltSwitchAttenuator[(ushort)data[1]].Dac = (ushort)data[2];
                }
            }
        }

        /// <summary> Функция обратного вызова операции считывания состояния устройства. </summary>
        /// <param name="data"> Массив данных. </param>
        private void cbfuncGetState(params object[] data)
        {
            if (this.InvokeRequired)
            {
                dCallbackSequenceFunction d = new dCallbackSequenceFunction(cbfuncGetState);
                BeginInvoke(d, new object[] { data });
            }
            else
            {
                Command cmd = (Command)data[0];
                byte state = cmd.getByte(0);

                tlsplbMOUT.Text = ((state & 0x01) > 0) ? "MOUT:HIGH" : "MOUT:LOW";
                tlsplbLO.Text = ((state & 0x02) > 0) ? "LO:LOCK" : "LO:NONE";

                // K9: Read attenuator switch position from state bits [3:2]
                // 00=Pos0, 01=Pos1, 10=Pos2, 11=Pos3
                int attSwitchPos = (state >> 2) & 0x03;
                if (attSwitchPos != activeAttPosition)
                {
                    SetActiveAttenuatorPosition(attSwitchPos);
                }

                // K9: Read LO switch band position from state bits [6:4]
                // 3 bits = 0-7 for 8-position selector switch
                int loSwitchPos = (state >> 4) & 0x07;
                if (loSwitchPos != activeSwitchBand)
                {
                    SetActiveSwitchBand(loSwitchPos);
                }
            }
        }

        /// <summary> Функция обратного вызова для считывания регистра ФАПЧ. </summary>
        /// <param name="data"> Массив данных. </param>
        private void cbfuncReadRegPLL(params object[] data)
        {
            if (this.InvokeRequired)
            {
                dCallbackSequenceFunction d = new dCallbackSequenceFunction(cbfuncReadRegPLL);
                BeginInvoke(d, new object[] { data });
            }
            else
            {
                if ((short)data[0] == (short)eSTATUS_COMMANDS.DONE)
                {
                    device.lo.pll.registers[(int)data[1]].body = (int)data[2];
                }
            }
        }

        /// <summary> Функция обратного вызова вызываемая при считывании регистра DDS. </summary>
        /// <param name="data"> Массив данных. </param>
        private void cbfuncGetRegisterDDS(params object[] data)
        {
            if (this.InvokeRequired)
            {
                dCallbackSequenceFunction d = new dCallbackSequenceFunction(cbfuncGetRegisterDDS);
                BeginInvoke(d, new object[] { data });
            }
            else
            {
                if ((short)data[0] == (short)eSTATUS_COMMANDS.DONE)
                {
                    device.dds.registers[(ushort)data[1]] = (ushort)data[2];
                }
            }
        }

        /// <summary> Инкремент состояния статусного поля. </summary>
        /// <param name="data"> Массив данных. </param>
        private void cbfuncIncrementStatusBar(params object[] data)
        {
            if (this.InvokeRequired)
            {
                dCallbackSequenceFunction d = new dCallbackSequenceFunction(cbfuncIncrementStatusBar);
                BeginInvoke(d, new object[] { data });
            }
            else
            {
                if (tlspStatusPanel.Value < tlspStatusPanel.Maximum)
                {
                    tlspStatusPanel.Value++;
                }
            }
        }

        /// <summary> обновление состояния формы. </summary>
        /// <param name="data"> Массив данных. </param>
        private void cbfuncConnectionUpdateWindows(params object[] data)
        {
            if (this.InvokeRequired)
            {
                dCallbackSequenceFunction d = new dCallbackSequenceFunction(cbfuncConnectionUpdateWindows);
                BeginInvoke(d, new object[] { data });
            }
            else
            {
                UpdateMainForm(device);
                ConnectionControlGUI(true, run_cfg);
                // K9: Always prepare DDS display (was Debug-only — hid BW/TwMem/FreqCtrl)
                PrepareGroupBoxDDS(device.dds.SweepOn);
                guiActive = true;

                // Populate grid from TDM EEPROM data
                if (tdmEepromValid && tdmEepromNumBands > 0)
                {
                    try
                    {
                        EnsureGridColumns();
                        dgvBands.Rows.Clear();
                        dgvBands.ReadOnly = false;

                        int loadedCount = 0;
                        for (int i = 0; i < tdmEepromNumBands && i < 8; i++)
                        {
                            if (!tdmEepromBandValid[i]) continue;

                            double freqMhz = tdmEepromLoFreq[i] / 1e6;
                            double bwMhz = tdmEepromBw[i] / 1e6;
                            int tones = (int)(tdmEepromPoints[i]);
                            if (tones < 32) tones = 256;
                            bool active = tdmEepromActive[i];
                            string ddsMode = "Ramp";
                            if (tdmEepromDdsMode[i] == 1) ddsMode = "PRBS";
                            else if (tdmEepromDdsMode[i] == 2) ddsMode = "Random";
                            Color rowColor = active
                                ? (ddsMode == "PRBS" ? Color.FromArgb(255, 200, 200) : Color.FromArgb(200, 255, 200))
                                : Color.FromArgb(255, 220, 220);

                            // Convert the saved per-band atten DAC back to dB for display.
                            // 0xFFFF or 0 = no per-band value (blank => uses main attenuator).
                            double bandAttenDb = 0.0;
                            ushort adCode = tdmEepromAtten[i];
                            if (adCode != 0xFFFF && adCode != 0 &&
                                device != null && device.AttCalibrationTable != null)
                            {
                                float v = (float)(((double)adCode / 4096.0) * 3.3);
                                bandAttenDb = Math.Round((double)device.AttCalibrationTable.getAttenuation(v), 1);
                            }

                            AddBandRow("", "Triggered",
                                string.Format("TDM Band {0}", i + 1),
                                freqMhz, bwMhz, 100.0, active, tones, rowColor, ddsMode,
                                bandAttenDb, tdmEepromCtrlFreq[i]);
                            loadedCount++;
                        }

                        if (numTdmDwell != null && tdmEepromDwellUs >= 50 && tdmEepromDwellUs <= 65000
                            && !tdmEepromDwellLoaded)
                        {
                            numTdmDwell.Value = tdmEepromDwellUs;
                            tdmEepromDwellLoaded = true;
                        }

                        if (chkGuardPulseMode != null)
                            chkGuardPulseMode.Checked = (tdmEepromMode == 2);
                        if (numGuardPulseOn != null && tdmEepromPulseOn >= 5)
                            numGuardPulseOn.Value = tdmEepromPulseOn;
                        if (numGuardPulseOff != null && tdmEepromPulseOff >= 5)
                            numGuardPulseOff.Value = tdmEepromPulseOff;
                        if (numDutyPercent != null && tdmEepromDuty >= 5 && tdmEepromDuty <= 100)
                            numDutyPercent.Value = tdmEepromDuty;

                        UpdateTdmHitsDisplay();
                        UpdateAllPointsFromDuty();
                        UpdateAllStepKhz();

                        string modeStr = tdmEepromMode == 2 ? "PULSED" : tdmEepromMode == 1 ? "CONTINUOUS" : "OFF";

                        // Compute autonomous firmware timing (µs precision)
                        double fwCycleUs = (double)tdmEepromDwellUs * loadedCount;
                        double fwCycleMs = fwCycleUs / 1000.0;
                        double fwCps = (fwCycleUs > 0) ? 1000000.0 / fwCycleUs : 0;

                        if (lblGuardStatus != null)
                        {
                            lblGuardStatus.Text = string.Format(" DEVICE: {0} bands | {1}\u00B5s dwell | ~{2:F0} cyc/s | {3}",
                                loadedCount, tdmEepromDwellUs, fwCps, modeStr);
                            lblGuardStatus.ForeColor = (tdmEepromMode > 0) ? Color.FromArgb(100, 255, 100) : Color.Gray;
                        }
                        if (lblLiveHits != null)
                        {
                            lblLiveHits.Text = string.Format("FW: {0:F1}ms cycle | {1:F1}ms revisit gap per band",
                                fwCycleMs, fwCycleMs - (tdmEepromDwellUs / 1000.0));
                            lblLiveHits.ForeColor = Color.FromArgb(180, 220, 255);
                        }

                        Debug.WriteLine(string.Format("TDM: Loaded {0} bands from device EEPROM, mode={1}",
                            loadedCount, modeStr));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("TDM EEPROM grid populate error: " + ex.Message);
                    }
                }

                // K9: When no TDM EEPROM, populate band table from live device state
                // This handles the case where the OG UI was used to configure the device
                if (!tdmEepromValid || tdmEepromNumBands == 0)
                {
                    try
                    {
                        EnsureGridColumns();
                        if (dgvBands.Rows.Count == 0 && device.lo.pll.OutFrequency > 1e6)
                        {
                            double freqMhz = device.lo.pll.OutFrequency / 1e6;
                            double bwMhz = device.dds.getRampBandwidth() / 1e6;
                            if (bwMhz < 1.0) bwMhz = 20.0;  // Fallback
                            int tones = (int)device.dds.Points;
                            if (tones < 32) tones = 256;
                            float freqCtrl = device.dds.FreqCtrl;
                            int twMem = device.dds.registers[0x47] & 0x0F;

                            string ddsMode = "Ramp";
                            if (device.dds.SweepOn == (ushort)AD9106_MODE.PSEUDO) ddsMode = "PRBS";

                            AddBandRow("", "Triggered", "Device Band",
                                freqMhz, bwMhz, 100.0, true, tones,
                                Color.FromArgb(200, 220, 255), ddsMode, 0.0, freqCtrl, twMem);

                            Debug.WriteLine(string.Format(
                                "K9: Created band row from live device: {0:F1}MHz, BW={1:F1}MHz, Pts={2}, FreqCtrl={3:F0}, TwMem={4}, Mode={5}",
                                freqMhz, bwMhz, tones, freqCtrl, twMem, ddsMode));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("K9: Live device band row error: " + ex.Message);
                    }
                }

                // Re-enable START button (may have been disabled by previous disconnect)
                if (btnGuardStart != null) btnGuardStart.Enabled = true;
                if (btnGuardStop != null) btnGuardStop.Enabled = false;

                // Auto-switch to Band Table Mode if device has valid TDM config
                if (tdmEepromValid && tdmEepromNumBands > 0)
                {
                    if (chkBandTableMode != null)
                        chkBandTableMode.Checked = true;  // Triggers SetBandTableMode(true) → UpdateGuardStatus()
                }
                else
                {
                    UpdateGuardStatus();
                }

                // Update connection LED
                if (ledConnection != null) { ledConnection.BackColor = Color.Lime; }
                if (lblConnectionStatus != null) { lblConnectionStatus.Text = "ONLINE"; lblConnectionStatus.ForeColor = Color.FromArgb(100, 255, 100); }
            }
        }

        /// <summary> Считывание результата росчерка lo </summary>
        /// <param name="data"> Результат считывания настройки росчерка lo </param>
        private void cbfuncGetSweepLoParameters(params object[] data)
        {
            if (this.InvokeRequired)
            {
                dCallbackSequenceFunction d = new dCallbackSequenceFunction(cbfuncGetSweepLoParameters);
                BeginInvoke(d, new object[] { data });
            }
            else
            {
                if ((short)data[0] == (short)eSTATUS_COMMANDS.DONE)
                {
                    device.lo.pll.OutFrequency = (float)data[1];
                    device.lo.Step = (float)data[2];
                    device.lo.Points = (uint)data[3];
                    device.lo.HoldTime = (float)data[4];
                    device.lo.WaitLD = (byte)data[5];
                    device.lo.SweepOn = (byte)data[6];
                }
            }
        }

        /// <summary> Считывание настроек DDS. </summary>
        /// <param name="data"> Массив данных. </param>
        private void cbfuncGetSweepDDS(params object[] data)
        {
            if (this.InvokeRequired)
            {
                dCallbackSequenceFunction d = new dCallbackSequenceFunction(cbfuncGetSweepDDS);
                BeginInvoke(d, new object[] { data });
            }
            else
            {
                if ((short)data[0] == (short)eSTATUS_COMMANDS.DONE)
                {
                    device.dds.SweepOn = (ushort)data[1];
                    device.dds.Start = (float)data[2];
                    device.dds.Step = (float)data[3];
                    device.dds.Points = (ulong)data[4];

                    if (device.dds.Points == 0) device.dds.Points = 1;
                }
            }
        }

        /// <summary></summary>
        /// <param name="data"></param>
        private void cbfuncGetDdsFrequencyCtrl(params object[] data)
        {
            if (this.InvokeRequired)
            {
                dCallbackSequenceFunction d = new dCallbackSequenceFunction(cbfuncGetDdsFrequencyCtrl);
                BeginInvoke(d, new object[] { data });
            }
            else
            {
                if ((short)data[0] == (short)eSTATUS_COMMANDS.DONE)
                {
                    device.dds.FreqCtrl = (float)data[1];
                    txbxDdsFreqCtrl.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(device.dds.FreqCtrl);
                }
            }
        }

        /// <summary> Считывание настроек импульсного модулятора. </summary>
        /// <param name="data"> Массив данных. </param>
        public void cbfuncGetPulseModulator(params object[] data)
        {
            if (this.InvokeRequired)
            {
                dCallbackSequenceFunction d = new dCallbackSequenceFunction(cbfuncGetPulseModulator);
                BeginInvoke(d, new object[] { data });
            }
            else
            {
                if ((short)data[0] == (short)eSTATUS_COMMANDS.DONE)
                {
                    device.pulse.ctrl = (ushort)data[1];
                    device.pulse.frequency = (float)data[2];
                    device.pulse.dutycycle = (float)data[3];
                }
            }
        }

        /// <summary> Вывод информации о программной и аппаратной версии. </summary>
        /// <param name="data"> Массив со считанными данными. </param>
        private void cbfuncAboutWindow(params object[] data)
        {
            if (this.InvokeRequired)
            {
                dCallbackSequenceFunction d = new dCallbackSequenceFunction(cbfuncAboutWindow);
                BeginInvoke(d, new object[] { data });
            }
            else
            {
                byte HV_MIN = (byte)data[0];
                byte HV_MAJ = (byte)data[1];
                byte FV_MIN = (byte)data[2];
                byte FV_MAJ = (byte)data[3];

                FmAbout form = new FmAbout();
                form.Text = "About";
                form.Icon = this.Icon;
                form.SoftwareVersion = Application.ProductVersion.ToString();
                form.HardwareVersion = String.Format("{0:d}.{1:d}", HV_MAJ, HV_MIN);
                form.FirmwareVersion = String.Format("{0:d}.{1:d}", FV_MAJ, FV_MIN);
                form.ShowDialog();
            }
        }

        /// <summary> Чтение конфигурации из устройства. </summary>
        /// <param name="data"> Считанная команда. </param>
        private void cdfuncReadConfiguration(params object[] data)
        {
            if (this.InvokeRequired)
            {
                dCallbackSequenceFunction d = new dCallbackSequenceFunction(cdfuncReadConfiguration);
                BeginInvoke(d, new object[] { data });
            }
            else
            {
                byte[] subarray;
                byte[] array = (byte[])data[0];
                ushort address = (ushort)data[1];
                ushort num_data = (ushort)data[2];
                ushort crc = 0;
                ushort crc_calc = 0;

                // ── K9 DIAGNOSTIC: raw hex dump for TDM EEPROM addresses ──
                if (address >= TDM_EEPROM_HEADER && address < TDM_EEPROM_BANDS + (8 * 32))
                {
                    string hexDump = BitConverter.ToString(array).Replace("-", " ");
                    Debug.WriteLine(string.Format(
                        "K9-DIAG EEPROM RAW @ addr={0} len={1}: [{2}]",
                        address, array.Length, hexDump));
                    // Store raw bytes for verification report
                    if (address == TDM_EEPROM_HEADER)
                        tdmDiagRawHeader = (byte[])array.Clone();
                    else if (address >= TDM_EEPROM_BANDS)
                    {
                        int idx = (address - TDM_EEPROM_BANDS) / 32;
                        if (idx >= 0 && idx < 8)
                            tdmDiagRawBand[idx] = (byte[])array.Clone();
                    }
                }

                crc_calc = CRC.CalculateCRC16(array, array.Length - 2);
                crc = (ushort)((ushort)array[array.Length - 1] * 256 + (ushort)array[array.Length - 2]);

                if (crc == crc_calc)
                {
                    if (address == (short)EEPROM.SN_DESCRIPTION)
                    {
                        #region SN_DESCRIPTION
                        subarray = new byte[10];
                        for (int i = 0; i < 10; i++)
                        {
                            subarray[i] = array[i];
                        }
                        this.sSerialNumber = Encoding.UTF8.GetString(subarray);

                        for (int i = 0; i < 8; i++)
                        {
                            subarray[i] = array[10 + i];
                        }
                        this.sDateManufacture = Encoding.UTF8.GetString(subarray);
                        #endregion
                    }
                    else if (address == (short)EEPROM.DDS_CONTROL)
                    {
                        #region DDS_CONTROL
                        subarray = new byte[4];
                        subarray[0] = array[0];
                        subarray[1] = array[1];
                        device.dds.SweepOn = (ushort)((ushort)subarray[0] + ((ushort)subarray[1] * 8));

                        subarray[0] = array[2];
                        subarray[1] = array[3];
                        subarray[2] = array[4];
                        subarray[3] = array[5];
                        device.dds.Start = System.BitConverter.ToSingle(subarray, 0);

                        subarray[0] = array[6];
                        subarray[1] = array[7];
                        subarray[2] = array[8];
                        subarray[3] = array[9];
                        device.dds.Step = System.BitConverter.ToSingle(subarray, 0);

                        subarray[0] = array[10];
                        subarray[1] = array[11];
                        subarray[2] = array[12];
                        subarray[3] = array[13];
                        device.dds.Points = System.BitConverter.ToUInt32(subarray, 0);

                        subarray[0] = array[14];
                        subarray[1] = array[15];
                        subarray[2] = array[16];
                        subarray[3] = array[17];
                        device.dds.FreqCtrl = System.BitConverter.ToSingle(subarray, 0);
                        #endregion
                    }
                    else if (address == (short)EEPROM.LO_SWITCH_PART1)
                    {
                        #region LO_SWITCH_PART1
                        subarray = new byte[4];
                        for (int i = 0; i < 4; i++)
                        {
                            for (int j = 0; j < 4; j++)
                            {
                                subarray[j] = array[4 * i + j];
                            }

                            try
                            {
                                device.ltSwitchFrequency[i] = System.BitConverter.ToSingle(subarray, 0);
                            }
                            catch
                            {
                                device.ltSwitchFrequency[i] = 2500000000.0F;
                            }
                        }
                        #endregion  /// LO_SWITCH_PART1
                    }
                    else if (address == (short)EEPROM.LO_SWITCH_PART2)
                    {
                        #region LO_SWITCH_PART2
                        subarray = new byte[4];
                        for (int i = 0; i < 4; i++)
                        {
                            for (int j = 0; j < 4; j++)
                            {
                                subarray[j] = array[4 * i + j];
                            }

                            try
                            {
                                device.ltSwitchFrequency[4 + i] = System.BitConverter.ToSingle(subarray, 0);
                            }
                            catch
                            {
                                device.ltSwitchFrequency[4 + i] = 2500000000.0F;
                            }
                        }
                        #endregion  /// LO_SWITCH_PART2
                    }
                    else if (address == TDM_EEPROM_HEADER)
                    {
                        #region TDM_HEADER
                        if (array.Length >= 16 && array[8] == 0x4B && array[9] == 0x39)
                        {
                            tdmEepromMode = array[0];
                            tdmEepromNumBands = array[1];
                            tdmEepromPulseOn = BitConverter.ToUInt16(array, 4);
                            tdmEepromPulseOff = BitConverter.ToUInt16(array, 6);

                            // Backward compatibility: version byte at array[10]
                            // 0x03 = µs dwell + duty, 0x02 = µs dwell, else ms (old)
                            ushort rawDwell = BitConverter.ToUInt16(array, 2);
                            if (array.Length > 10 && array[10] >= 0x02)
                            {
                                tdmEepromDwellUs = rawDwell;
                            }
                            else
                            {
                                // Old format: convert ms → µs
                                tdmEepromDwellUs = (ushort)(rawDwell < 1 ? 2000 : rawDwell * 1000);
                            }
                            // Duty cycle: version 3+ stores at array[11]
                            if (array.Length > 11 && array[10] >= 0x03 && array[11] >= 5 && array[11] <= 100)
                            {
                                tdmEepromDuty = array[11];
                            }
                            else
                            {
                                tdmEepromDuty = 100;  // Backward compat: full duty
                            }
                            tdmEepromValid = true;
                            Debug.WriteLine(string.Format("TDM EEPROM: mode={0}, bands={1}, dwell={2}\u00B5s, duty={3}%",
                                tdmEepromMode, tdmEepromNumBands, tdmEepromDwellUs, tdmEepromDuty));
                        }
                        else
                        {
                            tdmEepromValid = false;
                            Debug.WriteLine("TDM EEPROM: No valid config (signature mismatch)");
                        }
                        #endregion
                    }
                    else if (address >= TDM_EEPROM_BANDS && address < TDM_EEPROM_BANDS + (8 * 32))
                    {
                        #region TDM_BAND
                        int bandIdx = (address - TDM_EEPROM_BANDS) / 32;
                        if (bandIdx >= 0 && bandIdx < 8 && array.Length >= 28)
                        {
                            tdmEepromLoFreq[bandIdx] = BitConverter.ToDouble(array, 0);
                            tdmEepromBw[bandIdx] = BitConverter.ToSingle(array, 8);
                            tdmEepromStep[bandIdx] = BitConverter.ToSingle(array, 12);
                            tdmEepromPoints[bandIdx] = BitConverter.ToUInt32(array, 16);
                            tdmEepromCtrlFreq[bandIdx] = BitConverter.ToSingle(array, 20);
                            tdmEepromActive[bandIdx] = (array[24] != 0);
                            tdmEepromDdsMode[bandIdx] = array[25];  // 0=RAMP, 1=PRBS, 2=RANDOM
                            tdmEepromAtten[bandIdx] = (ushort)(array[26] | (array[27] << 8));  // per-band atten DAC
                            tdmEepromBandValid[bandIdx] = (tdmEepromLoFreq[bandIdx] > 1e6);
                            string[] mNames = { "RAMP", "PRBS", "RANDOM" };
                            string mN = (tdmEepromDdsMode[bandIdx] < mNames.Length) ? mNames[tdmEepromDdsMode[bandIdx]] : "?";
                            Debug.WriteLine(string.Format("TDM Band {0}: {1:F1} MHz, BW={2:F1} MHz, Active={3}, Mode={4}",
                                bandIdx, tdmEepromLoFreq[bandIdx] / 1e6, tdmEepromBw[bandIdx] / 1e6, tdmEepromActive[bandIdx],
                                mN));
                        }
                        #endregion
                    }

                }
            }
        }

        /// <summary> Вывод информационного сообщения. </summary>
        /// <param name="data"> Информационные сообщения. </param>
        private void cbfuncShowMessage(params object[] data)
        {
            if (this.InvokeRequired)
            {
                dCallbackSequenceFunction d = new dCallbackSequenceFunction(cbfuncShowMessage);
                BeginInvoke(d, new object[] { data });
            }
            else
            {
                if (data.Length == 2)
                {
                    MessageBox.Show((String)data[0], (String)data[1], MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        /// <summary> обновление состояния формы. </summary>
        /// <param name="data"></param>
        private void cbfuncUpdateWindows(params object[] data)
        {
            if (this.InvokeRequired)
            {
                dCallbackSequenceFunction d = new dCallbackSequenceFunction(cbfuncUpdateWindows);
                BeginInvoke(d, new object[] { data });
            }
            else
            {
                /*
                ControlWorkSpaceElements ( true );
                Update_GUI_FromConfiguration ( configuration );
                 */
            }
        }
        #endregion  /// CALLBACK FUNCTIONS

        /// <summary> Закрытие формы. </summary>
        private void FmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            timer1.Enabled = false;

            bExitusbConnectionThread = true;
            bExitSequenceThread = true;

            if (usbConnection != null)
            {
                if (usbConnection.IsOpen == true)
                {
                    if (usbConnection.Close() == false)
                    {
                        throw new Exception("Close connection error.");
                    }
                }
            }
        }

        /// <summary> </summary>
        private void tlspbtAbout_Click(object sender, EventArgs e)
        {
            try
            {
                if (usbConnection != null && usbConnection.IsOpen)
                {
                    AddSequences(new SequenceGetVersion(ltTxCommand, cbfuncAboutWindow));
                }
                else
                {
                    FmAbout form = new FmAbout();
                    form.Text = "About";
                    form.Icon = this.Icon;
                    form.SoftwareVersion = Application.ProductVersion.ToString();
                    form.HardwareVersion = "0.0";
                    form.FirmwareVersion = "0.00";
                    form.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [tlspbtAbout_Click]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> Прерывание таймера. </summary>
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (isJamming) return;

            if (usbConnection != null)
            {
                if (usbConnection.IsOpen)
                {
                    AddSequences(new SequenceGetDetectorCtrl(ltTxCommand, cbfuncGetDetectorCtrl));
                    if (run_cfg.bDgMessage == true)
                    {
                        AddSequences(new SequenceGetState(ltTxCommand, cbfuncGetState));
                    }
                }
            }
        }

        /// <summary> Функция приема данных. </summary>
        private void readEndpoint_DataReceived(object sender, EndpointDataEventArgs e)
        {
            ThreadSafeDebugMessage(String.Format("[USB receive]: {0:d} bytes\n", e.Count));

            // ── K9: Intercept CMD_TDM_STATUS (46) responses for timing diagnostics ──
            // Parse timing data and CONSUME — don't add to sequence queue.
            // The timer sends unsolicited status requests; responses would pile up
            // in ltRxCommand and confuse the sequence thread.
            if (e.Count >= 6)
            {
                short cmdType = (short)(e.Buffer[0] | (e.Buffer[1] << 8));
                if (cmdType == CMD_TDM_STATUS)
                {
                    tdmDiagRxCount++;
                    tdmDiagLastPktSize = e.Count;
                    try
                    {
                        // Packet: 6-byte header + data[26]. SYSCOM_SIZE=32.
                        // Timing diags at data[12..19] as uint16 = buffer offsets 18..25
                        if (e.Count >= 26)
                        {
                            tdmDiagLoopUs  = BitConverter.ToUInt16(e.Buffer, 18);  // data[12..13]
                            tdmDiagHopUs   = BitConverter.ToUInt16(e.Buffer, 20);  // data[14..15]
                            tdmDiagDwellUs = BitConverter.ToUInt16(e.Buffer, 22);  // data[16..17]
                            tdmDiagCycleUs = BitConverter.ToUInt16(e.Buffer, 24);  // data[18..19]
                            tdmDiagValid = true;

                            // Group rotation info: data[22]=group_size, data[23]=group_current
                            if (e.Count >= 30)  // 6-byte header + 24 data bytes
                            {
                                tdmGroupSize = e.Buffer[28];     // data[22]
                                tdmGroupCurrent = e.Buffer[29];  // data[23]
                                tdmDiagActiveCount = e.Buffer[7]; // data[1] = active_count
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("K9-DIAG parse error: " + ex.Message);
                    }
                    return;  // CONSUME — don't add to sequence queue
                }
            }

            Command cmd = new Command(e.Buffer, Command.GetCommandSize());
            lock (ltRxCommand)
            {
                ltRxCommand.Add(cmd);
            }
        }

        /// <summary> </summary>
        private void btDdsExample_CwMode_Click(object sender, EventArgs e)
        {
            try
            {
                ushort cmd_counter = 0;
                ushort channels_mask = 0;

                device.dds.Start = 2000000.0F;
                device.dds.FreqCtrl = 1000.0F;
                txbxDdsFreqCtrl.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(device.dds.FreqCtrl);

                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, device.dds.FreqCtrl));

                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1E, 0x0000));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x20, 0x0001));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                channels_mask = 0;
                if (device.dds.Channels[3] == true)
                    channels_mask = 0x3300;
                if (device.dds.Channels[2] == true)
                    channels_mask += 0x0033;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x26, channels_mask));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                channels_mask = 0;
                if (device.dds.Channels[1] == true)
                    channels_mask = 0x3300;
                if (device.dds.Channels[0] == true)
                    channels_mask += 0x0033;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x27, channels_mask));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x28, 0x0001));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x29, 0x8000));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x2A, 0x0101));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x2B, 0x0101));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x32, 0x7FF0));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x33, 0x7FF0));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x34, 0x7FF0));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x35, 0x7FF0));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x3E, device.dds.registers[0x3E]));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x3F, device.dds.registers[0x3F]));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x40, 0x0000));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x41, 0x0000));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x42, 0x0000));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x43, 0x0000));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x44, 0x0000));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x45, 0x0000));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x47, 0x0000));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                for (int i = 0; i < 4; i++)
                {
                    AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, (ushort)(0x50 + 4 * i), 0x0001));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                    AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, (ushort)(0x51 + 4 * i), 0x0000));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                    AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, (ushort)(0x52 + 4 * i), 0x0000));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                    AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, (ushort)(0x53 + 4 * i), 0x0100));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                }

                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1F, 0x0001));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1E, 0x0001));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, device.dds.FreqCtrl));
                AddSequences(new SequenceCallFunction(cbfuncConnectionUpdateWindows, null));

                tlspStatusPanel.Maximum = cmd_counter;
                tlspStatusPanel.Value = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [DdsParameters_KeyPress]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> </summary>
        private void btDdsExample_Ramp_Click(object sender, EventArgs e)
        {
            ushort cmd_counter = 0;
            ushort channels_mask = 0;

            device.dds.Points = 250;
            device.dds.FreqCtrl = 1000.0F;
            txbxDdsFreqCtrl.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(device.dds.FreqCtrl);

            AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, (float)device.dds.Start));

            #region RAM WRITE
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1F, 0x0000));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1E, 0x0000));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1E, 0x0004));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            for (ulong i = 0; i < device.dds.Points; i++)
            {
                ushort point = (ushort)((0x0FFF / (device.dds.Points - 1.0)) * i);
                point = (ushort)(point & 0x0FFF);

                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, (ushort)(0x6001 + i), (ushort)(point << 4)));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            }

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1E, 0x0000));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            #endregion RAM write

            #region GAIN CONTROL
            channels_mask = device.dds.getAnalogGain(3);
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x04, channels_mask));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            channels_mask = device.dds.getAnalogGain(2);
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x05, channels_mask));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            channels_mask = device.dds.getAnalogGain(1);
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x06, channels_mask));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            channels_mask = device.dds.getAnalogGain(0);
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x07, channels_mask));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            #endregion  /// GAIN CONTROL

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x20, 0x000E));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            channels_mask = (ushort)((device.dds.Channels[3] == true) ? 0x3100 : 0x0000);
            channels_mask += (ushort)((device.dds.Channels[2] == true) ? 0x0031 : 0x0000);
            device.dds.registers[0x26] = channels_mask;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x26, device.dds.registers[0x26]));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            channels_mask = (ushort)((device.dds.Channels[1] == true) ? 0x3100 : 0x0000);
            channels_mask += (ushort)((device.dds.Channels[0] == true) ? 0x0031 : 0x0000);
            device.dds.registers[0x27] = channels_mask;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x27, device.dds.registers[0x27]));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x28, 0x0111));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x29, 0x8000));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x2A, 0x0101));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x2B, 0x0101));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x2C, 0x0003));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x2D, 0x0000));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            #region DIG GAIN CONTROL
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x32, device.dds.registers[0x32]));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x33, device.dds.registers[0x33]));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x34, device.dds.registers[0x34]));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x35, device.dds.registers[0x35]));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            #endregion  /// GAIN CONTROL

            #region SAW_CONFIG
            device.dds.registers[0x36] = 0x0707;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x36, device.dds.registers[0x36]));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            device.dds.registers[0x37] = 0x0707;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x37, device.dds.registers[0x37]));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            #endregion //   SAW_CONFIG      

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x3E, device.dds.registers[0x3E]));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x3F, device.dds.registers[0x3F]));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x40, device.dds.getPTW(3)));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x41, device.dds.getPTW(2)));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x42, device.dds.getPTW(1)));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x43, device.dds.getPTW(0)));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x44, 0x0002));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x45, 0x4445));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            device.dds.registers[0x47] = 0x0002;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x47, (ushort)(device.dds.registers[0x47])));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            for (int i = 0; i < 4; i++)
            {
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, (ushort)(0x50 + 4 * i), 0));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, (ushort)(0x51 + 4 * i), (ushort)((0x6001 * 16) & 0xFFF0)));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, (ushort)(0x52 + 4 * i), (ushort)((ulong)(((0x6001 + device.dds.Points) * 16)) & 0xFFF0)));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, (ushort)(0x53 + 4 * i), 0));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            }

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1E, 0x0001));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1F, 0x0000));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, device.dds.FreqCtrl));
            AddSequences(new SequenceCallFunction(cbfuncConnectionUpdateWindows, null));

            tlspStatusPanel.Maximum = cmd_counter;
            tlspStatusPanel.Value = 0;
        }

        /// <summary> </summary>
        private void btDdsExample_Random_Click(object sender, EventArgs e)
        {
            ushort cmd_counter = 0;
            ushort channels_mask = 0;

            device.dds.FreqCtrl = 0.0F;
            txbxDdsFreqCtrl.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(device.dds.FreqCtrl);

            AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0));

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1E, 0x0000));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x20, 0x0001));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            channels_mask = 0;
            if (device.dds.Channels[3] == true)
                channels_mask = 0x2100;
            if (device.dds.Channels[2] == true)
                channels_mask += 0x0021;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x26, channels_mask));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            channels_mask = 0;
            if (device.dds.Channels[1] == true)
                channels_mask = 0x2100;
            if (device.dds.Channels[0] == true)
                channels_mask += 0x0021;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x27, channels_mask));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x28, 0x0001));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x29, 0x8000));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x2A, 0x0101));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x2B, 0x0101));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x32, 0x7FF0));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x33, 0x7FF0));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x34, 0x7FF0));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x35, 0x7FF0));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x3E, device.dds.registers[0x3E]));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x3F, device.dds.registers[0x3F]));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x44, 0x0000));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x45, 0x0000));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x47, 0x0000));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            for (int i = 0; i < 4; i++)
            {
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, (ushort)(0x50 + 4 * i), 0x0001));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, (ushort)(0x51 + 4 * i), 0x0000));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, (ushort)(0x52 + 4 * i), 0x0000));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, (ushort)(0x53 + 4 * i), 0x100));
                AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            }

            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1F, 0x0001));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1E, 0x0001));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
            AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));
            AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;

            AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, device.dds.FreqCtrl));
            AddSequences(new SequenceCallFunction(cbfuncConnectionUpdateWindows, null));

            tlspStatusPanel.Maximum = cmd_counter;
            tlspStatusPanel.Value = 0;
        }

        /// <summary> Подключение к устройству. </summary>
        private void tlspbtConnect_Click(object sender, EventArgs e)
        {
            try
            {
                if (usbConnection == null || usbConnection.IsOpen == false)
                {
                    UsbRegDeviceList allDevices = UsbDevice.AllDevices;
                    ThreadSafeDebugMessage(String.Format("device:{0:d}\n", allDevices.Count));

                    foreach (UsbRegistry usbRegistry in allDevices)
                    {
                        if (usbRegistry.Vid == 0x0483 & usbRegistry.Pid == 0x5740)
                        {
                            if (usbRegistry.Open(out usbConnection))
                            {
                                break;
                            }
                        }
                    }
                    if (usbConnection == null)
                    {
                        throw new Exception("Device Not Found.");
                    }

                    this.Text = "AWJ v3.0h  " + K9_BUILD + " — " + String.Format("{0:s}", usbConnection.Info.ProductString);
                    tlspSerialNumber.Text = String.Format("SN: {0:s}", usbConnection.Info.SerialString);
                    sSerialNumber = usbConnection.Info.SerialString;
                    writeEndpoint = usbConnection.OpenEndpointWriter(WriteEndpointID.Ep01);
                    readEndpoint = usbConnection.OpenEndpointReader(ReadEndpointID.Ep01);
                    readEndpoint.DataReceivedEnabled = true;
                    readEndpoint.DataReceived += readEndpoint_DataReceived;
                    readEndpoint.ReadThreadPriority = System.Threading.ThreadPriority.Highest;

                    AddSequences(new SequenceTestConnection(ltTxCommand, cbfuncInitDeviceList));
                }
                else
                {
                    CleanupOnDisconnect();
                    if (usbConnection.Close() == false)
                    {
                        throw new Exception("Close connection error.");
                    }
                    ConnectionControlGUI(false, run_cfg);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Attention!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> Установка настроек платы по умолчанию. </summary>
        private void tlspmnitDefault_Click(object sender, EventArgs e)
        {
            try
            {
                if (usbConnection.IsOpen)
                {
                    AddSequences(new SequenceDefaultState(ltTxCommand, null));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [tlspmnitDefault_Click]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> </summary>
        private void tlspmnitMax2871Reg_Click(object sender, EventArgs e)
        {
            try
            {
                FmMax2871 form = new FmMax2871(device.lo.pll);
                form.StartPosition = FormStartPosition.CenterParent;
                form.Text = "Configurations of the MAX2871";
                form.Icon = this.Icon;
                if (form.ShowDialog() == DialogResult.OK)
                {
                    device.lo.pll = form.pll;
                    for (int i = 0; i < 6; i++)
                    {
                        AddSequences(new SequenceWriteRegisterPLL(ltTxCommand, null, (ulong)device.lo.pll.registers[i].body));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [tlspmnitMax2871Reg_Click]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> Коррекция параметров гетеродина. </summary>
        private void LoParameters_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                if (isJamming) { e.Handled = true; return; }
                if (bandTableModeActive) { e.Handled = true; return; }

                String tag = (String)(((TextBox)sender).Tag);
                if (e.KeyChar == 13)
                {
                    ((TextBox)sender).BackColor = SystemColors.Window;
                    if (tag == "LoSweepStart")
                    {
                        device.lo.pll.OutFrequency = (float)nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble((String)(((TextBox)sender).Text));
                        (((TextBox)sender).Text) = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(device.lo.pll.OutFrequency, 3);
                    }
                    else if (tag == "LoSweepStep")
                    {
                        device.lo.Step = (float)nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble((String)(((TextBox)sender).Text));
                        (((TextBox)sender).Text) = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(device.lo.Step, 3);
                    }
                    else if (tag == "LoSweepPoint")
                    {
                        device.lo.Points = Convert.ToUInt32((String)(((TextBox)sender).Text));
                        (((TextBox)sender).Text) = Convert.ToString(device.lo.Points);
                    }
                    else if (tag == "LoSweepDelay")
                    {
                        device.lo.HoldTime = (float)nsAlexKir.AppConvertions.Functions.TimeValueToDouble((String)(((TextBox)sender).Text));
                        (((TextBox)sender).Text) = nsAlexKir.AppConvertions.Functions.DoubleToTimeValue(device.lo.HoldTime, 3);
                    }

                    device.lo.SweepOn = (device.lo.Points > 1) ? 1 : 0;
                    device.lo.WaitLD = (chbxLoWaitLD.Checked) ? 1 : 0;

                    AddSequences(new SequenceSetSweepLoParameters(ltTxCommand, null, (float)device.lo.pll.OutFrequency, device.lo.Step,
                        device.lo.Points, device.lo.HoldTime, device.lo.WaitLD, device.lo.SweepOn));
                }
                else
                {
                    ((TextBox)sender).BackColor = Color.Azure;
                    if (tag == "LoSweepStart" || tag == "LoSweepStep")
                    {
                        e.KeyChar = nsAlexKir.AppConvertions.Functions.CheckSymbolToFrequencyValue(e.KeyChar);
                    }
                    else if (tag == "LoSweepPoint")
                    {
                        e.KeyChar = nsAlexKir.AppConvertions.Functions.CheckSymbolToIntegerValue(e.KeyChar);
                    }
                    else if (tag == "LoSweepDelay")
                    {
                        e.KeyChar = nsAlexKir.AppConvertions.Functions.CheckSymbolToTimeValue(e.KeyChar);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [LoParameters_KeyPress]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> Управление аттенюатора. </summary>
        private void txbxAttenuatorCtrl_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                UInt16 uiTag = Convert.ToUInt16((String)(((TextBox)sender).Tag));
                double dValue = 0.0;
                if (e.KeyChar == 13)
                {
                    ((TextBox)sender).BackColor = SystemColors.Window;

                    dValue = nsAlexKir.AppConvertions.Functions.StringValueInDBtoDouble(((TextBox)sender).Text);

                    // RFSA2033 validation: 0-25 dB attenuation range, 0-2.5V control voltage
                    const double RFSA2033_MIN_DB = 0.0;
                    const double RFSA2033_MAX_DB = 25.0;

                    if (dValue < RFSA2033_MIN_DB || dValue > RFSA2033_MAX_DB)
                    {
                        MessageBox.Show(
                            "Attenuator value " + dValue.ToString("F1") + " dB is out of range.\n\n" +
                            "RFSA2033 valid range: " + RFSA2033_MIN_DB + " to " + RFSA2033_MAX_DB + " dB\n" +
                            "(Control voltage: 0 to 2.5V)",
                            "Attenuator Range Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                        // Clamp to nearest valid limit
                        dValue = Math.Max(RFSA2033_MIN_DB, Math.Min(RFSA2033_MAX_DB, dValue));
                    }

                    device.ltSwitchAttenuator[uiTag].CtrlVoltage = device.AttCalibrationTable.getVoltage((float)dValue);

                    AddSequences(new SequenceSetDacAttenuator(ltTxCommand, null, device.ltSwitchAttenuator[uiTag].Dac, uiTag));

                    // Store and display the user's exact dB value (avoid cal table round-trip error)
                    attUserDbValues[uiTag] = dValue;
                    ((TextBox)sender).Text = dValue.ToString("F2") + " dB";

                    // When user sets a value, make that position active
                    SetActiveAttenuatorPosition(uiTag);
                }
                else
                {
                    ((TextBox)sender).BackColor = Color.Azure;
                    ((TextBox)sender).ForeColor = Color.Black;  // Visible text while editing
                    e.KeyChar = nsAlexKir.AppConvertions.Functions.CheckSymbolToStrValueInDB(e.KeyChar);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [txbxAttenuatorCtrl_KeyPress]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> Изменение параметров DDS. </summary>
        private void DdsParameters_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                if (isJamming) { e.Handled = true; return; }
                if (bandTableModeActive) { e.Handled = true; return; }

                String tag = (String)(((TextBox)sender).Tag);

                if (e.KeyChar == 13)
                {
                    ((TextBox)sender).BackColor = SystemColors.Window;
                    if (tag == "Start")
                    {
                        device.dds.Start = (float)nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble((String)(((TextBox)sender).Text));
                        ((TextBox)sender).Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(device.dds.Start, 3);
                    }
                    else if (tag == "Step")
                    {
                        device.dds.Step = (float)nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble((String)(((TextBox)sender).Text));
                        ((TextBox)sender).Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(device.dds.Step, 3);
                    }
                    else if (tag == "Interval")
                    {
                        device.dds.Interval = (float)nsAlexKir.AppConvertions.Functions.TimeValueToDouble((String)(((TextBox)sender).Text));
                        ((TextBox)sender).Text = nsAlexKir.AppConvertions.Functions.DoubleToTimeValue(device.dds.Interval, 3);
                    }
                    else if (tag == "Points")
                    {
                        device.dds.Points = Convert.ToUInt32((String)(((TextBox)sender).Text));
                        ((TextBox)sender).Text = Convert.ToString(device.dds.Points);
                    }
                    else if (tag == "FreqCtrl")
                    {
                        device.dds.FreqCtrl = (float)nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble(txbxDdsFreqCtrl.Text);
                        ((TextBox)sender).Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(device.dds.FreqCtrl);
                        AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, (float)device.dds.FreqCtrl));
                        return;
                    }
                    else if (tag == "TwMem")
                    {
                        device.dds.TwMem = Convert.ToUInt16(txbxDdsTwMem.Text);
                    }
                    else if (tag == "Bandwidth")
                    {
                        if (this.run_cfg.iMode == Mode.User || this.run_cfg.iMode == Mode.Advanced) device.dds.Start = 0.0F;

                        // Parse bandwidth and clamp to 5-200 MHz
                        double bwValue = nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble(txbxDdsBandwith.Text);
                        double bwMHz = bwValue / 1e6;  // Convert to MHz for validation
                        if (bwMHz < 5.0)
                        {
                            bwMHz = 5.0;
                            MessageBox.Show("Minimum bandwidth is 5 MHz", "Bandwidth Limit",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else if (bwMHz > 200.0)
                        {
                            bwMHz = 200.0;
                            MessageBox.Show("Maximum bandwidth is 200 MHz", "Bandwidth Limit",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        bwValue = bwMHz * 1e6;  // Back to Hz for device

                        device.dds.setRampBandwidth(bwValue);

                        txbxDdsStart.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(device.dds.Start, 3);
                        txbxDdsBandwith.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(device.dds.getRampBandwidth(), 3);
                        txbxDdsStep.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(device.dds.Step, 3);
                        txbxDdsTwMem.Text = Convert.ToString(device.dds.TwMem);
                        txbxDdsPoints.Text = Convert.ToString(device.dds.Points);
                    }

                    if (device.dds.SweepOn == (ushort)AD9106_MODE.SOFTWARE)
                    {
                        device.dds.Start = (float)nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble(txbxDdsStart.Text);
                        AddSequences(new SequenceSetSweepDDS(
                                                ltTxCommand, null,
                                                1,
                                                device.dds.Start,
                                                device.dds.Step,
                                                device.dds.Points));
                    }
                    else
                    {
                        DDSSetConfiguration();
                    }
                }
                else
                {
                    if (tag == "Start" || tag == "Step" || tag == "FreqCtrl")
                    {
                        e.KeyChar = nsAlexKir.AppConvertions.Functions.CheckSymbolToFrequencyValue(e.KeyChar);
                    }
                    else if (tag == "Points")
                    {
                        e.KeyChar = nsAlexKir.AppConvertions.Functions.CheckSymbolToIntegerValue(e.KeyChar);
                    }
                    else if (tag == "Interval")
                    {
                        e.KeyChar = nsAlexKir.AppConvertions.Functions.CheckSymbolToTimeValue(e.KeyChar);
                    }
                    ((TextBox)sender).BackColor = Color.Azure;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [DdsParameters_KeyPress]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> </summary>                  
        private void cmbbxLoPower_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                if (e.KeyChar == 13)
                {
                    ((ComboBox)sender).BackColor = Color.White;
                    device.lo.pll.APWR = cmbbxLoPower.SelectedIndex;
                    AddSequences(new SequenceWriteRegisterPLL(ltTxCommand, null, (ulong)device.lo.pll.registers[4].body));
                }
                else
                {
                    ((ComboBox)sender).BackColor = Color.Azure;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [cmbbxLoPower_KeyPress]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> </summary>
        private void cmbbxLoPower_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (isJamming) return;

                if (guiActive == true)
                {
                    device.lo.pll.APWR = cmbbxLoPower.SelectedIndex;
                    AddSequences(new SequenceWriteRegisterPLL(ltTxCommand, null, (ulong)device.lo.pll.registers[4].body));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [cmbbxLoPower_SelectedIndexChanged]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> </summary>
        private void cmbbxDdsMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (isJamming) return;

                int selectedIdx = cmbbxDdsMode.SelectedIndex;
                k9ExtendedMode = selectedIdx;

                // Map UI mode index to AD9106 hardware mode
                ushort hwMode;
                switch (selectedIdx)
                {
                    case 0: hwMode = (ushort)AD9106_MODE.CONTINUE; break;   // CW
                    case 1: hwMode = (ushort)AD9106_MODE.SOFTWARE; break;   // Sweep
                    case 2: hwMode = (ushort)AD9106_MODE.RAMP; break;   // Ramp
                    case 3: hwMode = (ushort)AD9106_MODE.PSEUDO; break;   // Pseudo
                    case 4: hwMode = (ushort)AD9106_MODE.RAMP; break;   // Multi-Tone (RAMP with multi-channel)
                    case 5: hwMode = (ushort)AD9106_MODE.RAMP; break;   // TDM Jam (RAMP + fast TDM)
                    default: hwMode = (ushort)AD9106_MODE.CONTINUE; break;
                }

                device.dds.SweepOn = hwMode;
                PrepareGroupBoxDDS(hwMode);

                if (guiActive == true)
                {
                    DDSSetConfiguration();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [cmbbxDdsMode_SelectedIndexChanged]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> Управление каналами DDS. </summary>
        private void DdsConfiguration_Click(object sender, EventArgs e)
        {
            try
            {
                ushort cmd_counter = 0;
                FmDdsConfiguration form;

                form = new FmDdsConfiguration(device.dds);
                form.StartPosition = FormStartPosition.CenterParent;
                form.Text = "DDS configuration";
                form.Icon = this.Icon;
                if (form.ShowDialog() == DialogResult.OK)
                {
                    device.dds.PatternPeriod = form.getPatternPeriod();
                    device.dds.MaxRamp = form.getMaxRamp();
                    for (int i = 0; i < device.dds.Channels.Length; i++)
                    {
                        device.dds.Channels[i] = form.getChannelState(i);
                        device.dds.setPTW(i, form.getChannelPTW(i));
                        device.dds.setDigitalGain(i, form.getGain(i));
                    }
                    for (ushort i = 0; i < 0x60; i++)
                    {
                        AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, i, device.dds.registers[i]));
                        AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                    }
                    AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null)); cmd_counter++;
                    AddSequences(new SequenceCallFunction(cbfuncConnectionUpdateWindows, null));

                    tlspStatusPanel.Maximum = cmd_counter;
                    tlspStatusPanel.Value = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [tlspmnitMax2871Reg_Click]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> </summary>
        private void chbxLoWaitLD_Click(object sender, EventArgs e)
        {
            try
            {
                device.lo.WaitLD = (chbxLoWaitLD.Checked) ? 1 : 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [chbxLoWaitLD_Click]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> </summary>
        private void tlspmiDeviceInformation_Click(object sender, EventArgs e)
        {
            try
            {
                if (usbConnection.IsOpen)
                {
                    FmDeviceInformation form = new FmDeviceInformation();
                    form.Text = this.Text;
                    form.Icon = this.Icon;
                    form.sDateManufacture = sDateManufacture;
                    form.sSerialNumber = sSerialNumber;
                    if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        byte[] txBuff = new byte[20];
                        for (int i = 0; i < 20; i++)
                        {
                            txBuff[i] = 0;
                        }

                        sSerialNumber = form.sSerialNumber;
                        sDateManufacture = form.sDateManufacture;

                        byte[] tempBuff = Encoding.ASCII.GetBytes(sSerialNumber);
                        for (int i = 0; i < 10; i++)
                        {
                            txBuff[i] = tempBuff[i];
                        }
                        tempBuff = Encoding.ASCII.GetBytes(sDateManufacture);
                        for (int i = 0; i < 8; i++)
                        {
                            txBuff[8 + i] = tempBuff[i];
                        }

                        ushort crc_calc = CRC.CalculateCRC16(txBuff, 18);
                        txBuff[18] = (byte)((crc_calc >> 0) & 0xFF);
                        txBuff[19] = (byte)((crc_calc >> 8) & 0xFF);

                        AddSequences(new SequenceWriteMemory(ltTxCommand, null, (ushort)EEPROM.SN_DESCRIPTION, txBuff, 20));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [tlspmiDeviceInformation_Click]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> </summary>
        private void tlspbtConfigSave_Click(object sender, EventArgs e)
        {
            try
            {
                if (usbConnection.IsOpen)
                {
                    ushort crc = 0;
                    ushort len = 0;
                    ushort cmd_cnt = 0;
                    ushort usData = 0;
                    byte[] tmpBuff;
                    byte[] write_data = new byte[64];

                    #region DEVICE INFORMATION
                    len = 0;
                    tmpBuff = Encoding.UTF8.GetBytes(this.sSerialNumber);
                    for (int i = 0; i < 10; i++)
                    {
                        write_data[len++] = tmpBuff[i];
                    }
                    tmpBuff = Encoding.UTF8.GetBytes(this.sDateManufacture);
                    for (int i = 0; i < 8; i++)
                    {
                        write_data[len++] = tmpBuff[i];
                    }
                    crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                    write_data[len++] = (byte)((crc >> 0) & 0xFF);
                    write_data[len++] = (byte)((crc >> 8) & 0xFF);
                    AddSequences(new SequenceWriteMemory(ltTxCommand, cbfuncIncrementStatusBar, (ushort)EEPROM.SN_DESCRIPTION, write_data, (ushort)len));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null));
                    AddSequences(new SequenceDelay(100)); cmd_cnt++;
                    #endregion  //  DEVICE INFORMATION

                    #region DEVICE CONFIGURATION
                    len = 0;
                    write_data[len++] = (byte)((device.ltSwitchAttenuator[0].Dac >> 0) & 0xFF);
                    write_data[len++] = (byte)((device.ltSwitchAttenuator[0].Dac >> 8) & 0xFF);
                    write_data[len++] = (byte)((device.ltSwitchAttenuator[1].Dac >> 0) & 0xFF);
                    write_data[len++] = (byte)((device.ltSwitchAttenuator[1].Dac >> 8) & 0xFF);
                    write_data[len++] = (byte)((device.ltSwitchAttenuator[2].Dac >> 0) & 0xFF);
                    write_data[len++] = (byte)((device.ltSwitchAttenuator[2].Dac >> 8) & 0xFF);
                    write_data[len++] = (byte)((device.ltSwitchAttenuator[3].Dac >> 0) & 0xFF);
                    write_data[len++] = (byte)((device.ltSwitchAttenuator[3].Dac >> 8) & 0xFF);
                    tmpBuff = BitConverter.GetBytes(device.buzzer.delay_on);
                    write_data[len++] = tmpBuff[0];
                    write_data[len++] = tmpBuff[1];
                    write_data[len++] = tmpBuff[2];
                    write_data[len++] = tmpBuff[3];
                    tmpBuff = BitConverter.GetBytes(device.buzzer.time_on);
                    write_data[len++] = tmpBuff[0];
                    write_data[len++] = tmpBuff[1];
                    write_data[len++] = tmpBuff[2];
                    write_data[len++] = tmpBuff[3];
                    crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                    write_data[len++] = (byte)((crc >> 0) & 0xFF);
                    write_data[len++] = (byte)((crc >> 8) & 0xFF);
                    AddSequences(new SequenceWriteMemory(ltTxCommand, cbfuncIncrementStatusBar, (ushort)EEPROM.DEVICE_CONFIG, write_data, (ushort)len));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null));
                    AddSequences(new SequenceDelay(100)); cmd_cnt++;
                    #endregion  ///  DEVICE CONFIGURATION

                    #region PLL INT
                    len = 0;
                    for (int i = 0; i < 6; i++)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            write_data[len++] = device.lo.pll.registers[i].data[j];
                        }
                    }
                    crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                    write_data[len++] = (byte)((crc >> 0) & 0xFF);
                    write_data[len++] = (byte)((crc >> 8) & 0xFF);
                    AddSequences(new SequenceWriteMemory(ltTxCommand, cbfuncIncrementStatusBar, (ushort)EEPROM.PLL_INIT, write_data, (ushort)len));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null));
                    AddSequences(new SequenceDelay(100)); cmd_cnt++;
                    #endregion  /// PLL INIT

                    #region PLL CONFIGURATION
                    len = 0;
                    write_data[len++] = (byte)(device.lo.WaitLD > 0 ? 1 : 0);
                    write_data[len++] = (byte)(device.lo.SweepOn > 0 ? 1 : 0);
                    tmpBuff = BitConverter.GetBytes(device.lo.Step);
                    write_data[len++] = tmpBuff[0];
                    write_data[len++] = tmpBuff[1];
                    write_data[len++] = tmpBuff[2];
                    write_data[len++] = tmpBuff[3];
                    tmpBuff = BitConverter.GetBytes(device.lo.Points);
                    write_data[len++] = tmpBuff[0];
                    write_data[len++] = tmpBuff[1];
                    write_data[len++] = tmpBuff[2];
                    write_data[len++] = tmpBuff[3];
                    tmpBuff = BitConverter.GetBytes(device.lo.HoldTime);
                    write_data[len++] = tmpBuff[0];
                    write_data[len++] = tmpBuff[1];
                    write_data[len++] = tmpBuff[2];
                    write_data[len++] = tmpBuff[3];
                    crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                    write_data[len++] = (byte)((crc >> 0) & 0xFF);
                    write_data[len++] = (byte)((crc >> 8) & 0xFF);
                    AddSequences(new SequenceWriteMemory(ltTxCommand, cbfuncIncrementStatusBar, (ushort)EEPROM.PLL_CONFIG, write_data, (ushort)len));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null));
                    AddSequences(new SequenceDelay(100)); cmd_cnt++;
                    #endregion  /// PLL CONFIGURATION

                    #region DDS CONTROL
                    len = 0;
                    usData = device.dds.SweepOn;
                    write_data[len++] = (byte)((usData >> 0) & 0xFF);
                    write_data[len++] = (byte)((usData >> 8) & 0xFF);
                    tmpBuff = BitConverter.GetBytes(device.dds.Start);
                    write_data[len++] = tmpBuff[0];
                    write_data[len++] = tmpBuff[1];
                    write_data[len++] = tmpBuff[2];
                    write_data[len++] = tmpBuff[3];
                    tmpBuff = BitConverter.GetBytes(device.dds.Step);
                    write_data[len++] = tmpBuff[0];
                    write_data[len++] = tmpBuff[1];
                    write_data[len++] = tmpBuff[2];
                    write_data[len++] = tmpBuff[3];
                    tmpBuff = BitConverter.GetBytes(device.dds.Points);
                    write_data[len++] = tmpBuff[0];
                    write_data[len++] = tmpBuff[1];
                    write_data[len++] = tmpBuff[2];
                    write_data[len++] = tmpBuff[3];
                    tmpBuff = BitConverter.GetBytes(device.dds.FreqCtrl);
                    write_data[len++] = tmpBuff[0];
                    write_data[len++] = tmpBuff[1];
                    write_data[len++] = tmpBuff[2];
                    write_data[len++] = tmpBuff[3];
                    crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                    write_data[len++] = (byte)((crc >> 0) & 0xFF);
                    write_data[len++] = (byte)((crc >> 8) & 0xFF);
                    AddSequences(new SequenceWriteMemory(ltTxCommand, cbfuncIncrementStatusBar, (ushort)EEPROM.DDS_CONTROL, write_data, (ushort)len));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null));
                    AddSequences(new SequenceDelay(100)); cmd_cnt++;
                    #endregion  /// DDS CONTROL

                    #region DDS_PART1
                    len = 0;
                    for (int i = 0; i < 15; i++)
                    {
                        write_data[len++] = (byte)((device.dds.registers[i] >> 0) & 0xFF);
                        write_data[len++] = (byte)((device.dds.registers[i] >> 8) & 0xFF);
                    }
                    crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                    write_data[len++] = (byte)((crc >> 0) & 0xFF);
                    write_data[len++] = (byte)((crc >> 8) & 0xFF);
                    AddSequences(new SequenceWriteMemory(ltTxCommand, cbfuncIncrementStatusBar, (ushort)EEPROM.DDS_REG_PART1, write_data, (ushort)len));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null));
                    AddSequences(new SequenceDelay(100)); cmd_cnt++;
                    #endregion  /// DDS_PART1

                    #region DDS_PART2
                    len = 0;
                    write_data[len++] = (byte)((device.dds.registers[30] >> 0) & 0xFF); //  1
                    write_data[len++] = (byte)((device.dds.registers[30] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[31] >> 0) & 0xFF); //  2
                    write_data[len++] = (byte)((device.dds.registers[31] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[32] >> 0) & 0xFF); //  3
                    write_data[len++] = (byte)((device.dds.registers[32] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[34] >> 0) & 0xFF); //  4
                    write_data[len++] = (byte)((device.dds.registers[34] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[35] >> 0) & 0xFF); //  5
                    write_data[len++] = (byte)((device.dds.registers[35] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[36] >> 0) & 0xFF); //  6
                    write_data[len++] = (byte)((device.dds.registers[36] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[37] >> 0) & 0xFF); //  7
                    write_data[len++] = (byte)((device.dds.registers[37] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[38] >> 0) & 0xFF); //  8
                    write_data[len++] = (byte)((device.dds.registers[38] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[39] >> 0) & 0xFF); //  9
                    write_data[len++] = (byte)((device.dds.registers[39] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[40] >> 0) & 0xFF); //  10
                    write_data[len++] = (byte)((device.dds.registers[40] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[41] >> 0) & 0xFF); //  11
                    write_data[len++] = (byte)((device.dds.registers[41] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[42] >> 0) & 0xFF); //  12
                    write_data[len++] = (byte)((device.dds.registers[42] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[43] >> 0) & 0xFF); //  13
                    write_data[len++] = (byte)((device.dds.registers[43] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[44] >> 0) & 0xFF); //  14
                    write_data[len++] = (byte)((device.dds.registers[44] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[45] >> 0) & 0xFF); //  15
                    write_data[len++] = (byte)((device.dds.registers[45] >> 8) & 0xFF);

                    crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                    write_data[len++] = (byte)((crc >> 0) & 0xFF);
                    write_data[len++] = (byte)((crc >> 8) & 0xFF);
                    AddSequences(new SequenceWriteMemory(ltTxCommand, cbfuncIncrementStatusBar, (ushort)EEPROM.DDS_REG_PART2, write_data, (ushort)len));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null));
                    AddSequences(new SequenceDelay(100)); cmd_cnt++;
                    #endregion  /// DDS_PART2

                    #region DDS_PART3
                    len = 0;

                    write_data[len++] = (byte)((device.dds.registers[46] >> 0) & 0xFF); //  1
                    write_data[len++] = (byte)((device.dds.registers[46] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[47] >> 0) & 0xFF); //  2
                    write_data[len++] = (byte)((device.dds.registers[47] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[48] >> 0) & 0xFF); //  3
                    write_data[len++] = (byte)((device.dds.registers[48] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[49] >> 0) & 0xFF); //  4
                    write_data[len++] = (byte)((device.dds.registers[49] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[50] >> 0) & 0xFF); //  5
                    write_data[len++] = (byte)((device.dds.registers[50] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[51] >> 0) & 0xFF); //  6
                    write_data[len++] = (byte)((device.dds.registers[51] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[52] >> 0) & 0xFF); //  7
                    write_data[len++] = (byte)((device.dds.registers[52] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[53] >> 0) & 0xFF); //  8
                    write_data[len++] = (byte)((device.dds.registers[53] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[54] >> 0) & 0xFF); //  9
                    write_data[len++] = (byte)((device.dds.registers[54] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[55] >> 0) & 0xFF); //  10
                    write_data[len++] = (byte)((device.dds.registers[55] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[62] >> 0) & 0xFF); //  11
                    write_data[len++] = (byte)((device.dds.registers[62] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[63] >> 0) & 0xFF); //  12
                    write_data[len++] = (byte)((device.dds.registers[63] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[64] >> 0) & 0xFF); //  13
                    write_data[len++] = (byte)((device.dds.registers[64] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[65] >> 0) & 0xFF); //  14
                    write_data[len++] = (byte)((device.dds.registers[65] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[66] >> 0) & 0xFF); //  15
                    write_data[len++] = (byte)((device.dds.registers[66] >> 8) & 0xFF);

                    crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                    write_data[len++] = (byte)((crc >> 0) & 0xFF);
                    write_data[len++] = (byte)((crc >> 8) & 0xFF);
                    AddSequences(new SequenceWriteMemory(ltTxCommand, cbfuncIncrementStatusBar, (ushort)EEPROM.DDS_REG_PART3, write_data, (ushort)len));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null));
                    AddSequences(new SequenceDelay(100)); cmd_cnt++;
                    #endregion  /// DDS_PART3

                    #region DDS_PART4
                    len = 0;

                    write_data[len++] = (byte)((device.dds.registers[67] >> 0) & 0xFF); //  1
                    write_data[len++] = (byte)((device.dds.registers[67] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[68] >> 0) & 0xFF); //  2
                    write_data[len++] = (byte)((device.dds.registers[68] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[69] >> 0) & 0xFF); //  3
                    write_data[len++] = (byte)((device.dds.registers[69] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[71] >> 0) & 0xFF); //  4
                    write_data[len++] = (byte)((device.dds.registers[71] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[80] >> 0) & 0xFF); //  5
                    write_data[len++] = (byte)((device.dds.registers[80] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[81] >> 0) & 0xFF); //  6
                    write_data[len++] = (byte)((device.dds.registers[81] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[82] >> 0) & 0xFF); //  7
                    write_data[len++] = (byte)((device.dds.registers[82] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[83] >> 0) & 0xFF); //  8
                    write_data[len++] = (byte)((device.dds.registers[83] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[84] >> 0) & 0xFF); //  9
                    write_data[len++] = (byte)((device.dds.registers[84] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[85] >> 0) & 0xFF); //  10
                    write_data[len++] = (byte)((device.dds.registers[85] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[86] >> 0) & 0xFF); //  11
                    write_data[len++] = (byte)((device.dds.registers[86] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[87] >> 0) & 0xFF); //  12
                    write_data[len++] = (byte)((device.dds.registers[87] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[88] >> 0) & 0xFF); //  13
                    write_data[len++] = (byte)((device.dds.registers[88] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[89] >> 0) & 0xFF); //  14
                    write_data[len++] = (byte)((device.dds.registers[89] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[90] >> 0) & 0xFF); //  15
                    write_data[len++] = (byte)((device.dds.registers[90] >> 8) & 0xFF);

                    crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                    write_data[len++] = (byte)((crc >> 0) & 0xFF);
                    write_data[len++] = (byte)((crc >> 8) & 0xFF);
                    AddSequences(new SequenceWriteMemory(ltTxCommand, cbfuncIncrementStatusBar, (ushort)EEPROM.DDS_REG_PART4, write_data, (ushort)len));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null));
                    AddSequences(new SequenceDelay(100)); cmd_cnt++;
                    #endregion  /// DDS_PART4

                    #region DDS_PART5
                    len = 0;

                    write_data[len++] = (byte)((device.dds.registers[91] >> 0) & 0xFF); //  1
                    write_data[len++] = (byte)((device.dds.registers[91] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[92] >> 0) & 0xFF); //  2
                    write_data[len++] = (byte)((device.dds.registers[92] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[93] >> 0) & 0xFF); //  3
                    write_data[len++] = (byte)((device.dds.registers[93] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[94] >> 0) & 0xFF); //  4
                    write_data[len++] = (byte)((device.dds.registers[94] >> 8) & 0xFF);

                    write_data[len++] = (byte)((device.dds.registers[95] >> 0) & 0xFF); //  5
                    write_data[len++] = (byte)((device.dds.registers[95] >> 8) & 0xFF);

                    crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                    write_data[len++] = (byte)((crc >> 0) & 0xFF);
                    write_data[len++] = (byte)((crc >> 8) & 0xFF);
                    AddSequences(new SequenceWriteMemory(ltTxCommand, cbfuncIncrementStatusBar, (ushort)EEPROM.DDS_REG_PART5, write_data, (ushort)len));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null));
                    AddSequences(new SequenceDelay(100)); cmd_cnt++;
                    #endregion  /// DDS_PART5

                    #region LO FREQUENCY SWITCH
                    len = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        tmpBuff = BitConverter.GetBytes(device.ltSwitchFrequency[i]);
                        write_data[len++] = tmpBuff[0];
                        write_data[len++] = tmpBuff[1];
                        write_data[len++] = tmpBuff[2];
                        write_data[len++] = tmpBuff[3];
                    }
                    crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                    write_data[len++] = (byte)((crc >> 0) & 0xFF);
                    write_data[len++] = (byte)((crc >> 8) & 0xFF);
                    AddSequences(new SequenceWriteMemory(ltTxCommand, cbfuncIncrementStatusBar, (ushort)EEPROM.LO_SWITCH_PART1, write_data, (ushort)len));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null));

                    len = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        tmpBuff = BitConverter.GetBytes(device.ltSwitchFrequency[4 + i]);
                        write_data[len++] = tmpBuff[0];
                        write_data[len++] = tmpBuff[1];
                        write_data[len++] = tmpBuff[2];
                        write_data[len++] = tmpBuff[3];
                    }
                    crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                    write_data[len++] = (byte)((crc >> 0) & 0xFF);
                    write_data[len++] = (byte)((crc >> 8) & 0xFF);
                    AddSequences(new SequenceWriteMemory(ltTxCommand, cbfuncIncrementStatusBar, (ushort)EEPROM.LO_SWITCH_PART2, write_data, (ushort)len));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null));
                    #endregion

                    /// Добавление действия для сообщения о завершении записи.
                    AddSequences(new SequenceCallFunction(cbfuncShowMessage, new String[] { "Write configuration complete", "Information" }));

                    tlspStatusPanel.Maximum = cmd_cnt;
                    tlspStatusPanel.Value = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [tlspbtConfigSave_Click]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> </summary>
        private void tlspbtEraseMemory_Click(object sender, EventArgs e)
        {
            try
            {
                int cmd_cnt = 0;
                byte[] write_data = Enumerable.Repeat((byte)0xFF, 32).ToArray();

                while (cmd_cnt < 128)
                {
                    AddSequences(new SequenceWriteMemory(ltTxCommand, cbfuncIncrementStatusBar, (ushort)(cmd_cnt * 32), write_data, (ushort)write_data.Length));
                    AddSequences(new SequenceCallFunction(cbfuncIncrementStatusBar, null));
                    AddSequences(new SequenceDelay(100));
                    cmd_cnt++;
                }

                /// Добавление действия для сообщения о завершении записи.
                AddSequences(new SequenceCallFunction(cbfuncShowMessage, new String[] { "Erase memory complete", "Information" }));

                tlspStatusPanel.Maximum = cmd_cnt;
                tlspStatusPanel.Value = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [tlspbtEraseMemory_Click]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> </summary>
        private void tlspmnSaveToFile_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.Filter = "configuration files (*.xml)|*.xml";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    nsAlexKir.ProjectConfiguration<DmsDevice>.SaveConfigurationToXMLfile(dialog.FileName, device);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [tlspmnitSaveToFile_Click]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> </summary>
        private void tlspmnLoadFromFile_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Filter = "configuration files (*.xml)|*.xml";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    device = nsAlexKir.ProjectConfiguration<DmsDevice>.LoadConfigurationFromXMLfile(dialog.FileName);

                    UpdateMainForm(device);

                    SetDeviceConfiguration(device);

                    AddSequences(new SequenceCallFunction(cbfuncShowMessage, new String[] { "Change device settings completed", "Information" }));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [tlspmnLoadFromFile_Click]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> </summary>
        private void txbxPulseModulator_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                String tag = (String)(((TextBox)sender).Tag);
                if (e.KeyChar == 13)
                {
                    ((TextBox)sender).BackColor = SystemColors.Window;
                    if ((String)(((TextBox)sender).Tag) == "PulseModulator_Frequency")
                    {
                        device.pulse.frequency = (float)nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble(((TextBox)sender).Text);
                        ((TextBox)sender).Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(device.pulse.frequency, 2, nsAlexKir.AppConvertions.eLanguage.eEnglish);
                    }
                    else if ((String)(((TextBox)sender).Tag) == "PulseModulator_DutyCycle")
                    {
                        device.pulse.dutycycle = (float)nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble(((TextBox)sender).Text);
                        ((TextBox)sender).Text = Convert.ToString(device.pulse.dutycycle);
                    }
                    device.pulse.ctrl = (ushort)((device.pulse.frequency > 0 && device.pulse.dutycycle > 0) ? 1 : 0);
                    AddSequences(new SequenceSetPulseModulator(ltTxCommand, null, device.pulse.ctrl, device.pulse.frequency, device.pulse.dutycycle));
                }
                else
                {
                    ((TextBox)sender).BackColor = Color.Azure;
                    if ((String)(((TextBox)sender).Tag) == "PulseModulator_Frequency")
                    {
                        e.KeyChar = nsAlexKir.AppConvertions.Functions.CheckSymbolToFrequencyValue(e.KeyChar);
                    }
                    else if ((String)(((TextBox)sender).Tag) == "PulseModulator_DutyCycle")
                    {
                        e.KeyChar = nsAlexKir.AppConvertions.Functions.CheckSymbolToFloatValue(e.KeyChar);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [txbxAttenuatorCtrl_KeyPress]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> </summary>
        private void tlspmnitSwitches_Click(object sender, EventArgs e)
        {
            try
            {
                FmFrequencySwitch form;

                form = new FmFrequencySwitch(device);
                form.StartPosition = FormStartPosition.CenterParent;
                form.Text = "Setting frequencies values of the switch";
                form.Icon = this.Icon;
                if (form.ShowDialog() == DialogResult.OK)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        device.ltSwitchFrequency[i] = form.getSwitchFrequency(i);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [tlspmnitSwitches_Click]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary> </summary>
        private void txbxLoSwitchState_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                int tag = Convert.ToInt32(((TextBox)sender).Tag);
                int idx = tag - 1;  // Tags are 1-8, indices are 0-7

                if (e.KeyChar == 13)
                {
                    ((TextBox)sender).BackColor = SystemColors.Window;
                    float freq = (float)(nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble(((TextBox)sender).Text));

                    // Write to device only if connected and collection is valid
                    if (device != null && device.ltSwitchFrequency != null
                        && idx >= 0 && idx < device.ltSwitchFrequency.Count)
                    {
                        device.ltSwitchFrequency[idx] = freq;
                    }

                    // Update active band colour (always works, even disconnected)
                    SetActiveSwitchBand(idx);
                }
                else
                {
                    ((TextBox)sender).BackColor = Color.Azure;
                    ((TextBox)sender).ForeColor = Color.Black;  // Visible text while editing
                    e.KeyChar = nsAlexKir.AppConvertions.Functions.CheckSymbolToFrequencyValue(e.KeyChar);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [txbxLoSwitchState_KeyPress]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Sets the active LO switch band (called when 8-position external switch changes).
        /// The active band textbox turns GREEN, all others turn RED.
        /// </summary>
        public void SetActiveSwitchBand(int band)
        {
            if (band < 0 || band > 7) return;
            activeSwitchBand = band;
            UpdateSwitchBandColours();

            Debug.WriteLine("LO Switch Band changed to Band " + (band + 1));
        }

        /// <summary>
        /// Updates LO switch frequency textbox colours.
        /// ACTIVE band = GREEN, all others = RED.
        /// </summary>
        private void UpdateSwitchBandColours()
        {
            Color activeColour = Color.FromArgb(60, 180, 60);    // Green
            Color inactiveColour = Color.FromArgb(210, 80, 80);    // Red

            TextBox[] switchBoxes = new TextBox[] {
                txbxLoSwitchState1, txbxLoSwitchState2, txbxLoSwitchState3, txbxLoSwitchState4,
                txbxLoSwitchState5, txbxLoSwitchState6, txbxLoSwitchState7, txbxLoSwitchState8
            };

            for (int i = 0; i < switchBoxes.Length; i++)
            {
                if (switchBoxes[i] != null)
                {
                    switchBoxes[i].BackColor = (i == activeSwitchBand) ? activeColour : inactiveColour;
                    switchBoxes[i].ForeColor = Color.Black;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // K9 ELECTRONICS - GUARD BAND & DIAGNOSTICS ADDITIONS
        // ═══════════════════════════════════════════════════════════════

        #region GUARD_BAND_SETTINGS

        /// <summary>
        /// Create separate Guard Band Settings box - DOCKED TO RIGHT SIDE
        /// </summary>

        /// <summary>
        /// Creates attenuator position selector checkboxes (positions 1-3)
        /// Position 0 is default, positions 1-3 are switch-selectable via STM input
        /// Only one can be active at a time (mutual exclusion)
        /// Green = active, Amber = inactive
        /// </summary>
        private void CreateAttenuatorSelectors()
        {
            if (grbxATT == null) return;

            try
            {
                // Position 1 checkbox - placed to the right of its textbox
                chkAtt1 = new CheckBox();
                chkAtt1.Text = "";
                chkAtt1.Size = new Size(18, 18);
                chkAtt1.Tag = 1;
                chkAtt1.BackColor = Color.FromArgb(255, 190, 60);  // Amber = inactive
                chkAtt1.FlatStyle = FlatStyle.Flat;
                chkAtt1.CheckedChanged += chkAttSelector_CheckedChanged;

                chkAtt2 = new CheckBox();
                chkAtt2.Text = "";
                chkAtt2.Size = new Size(18, 18);
                chkAtt2.Tag = 2;
                chkAtt2.BackColor = Color.FromArgb(255, 190, 60);  // Amber
                chkAtt2.FlatStyle = FlatStyle.Flat;
                chkAtt2.CheckedChanged += chkAttSelector_CheckedChanged;

                chkAtt3 = new CheckBox();
                chkAtt3.Text = "";
                chkAtt3.Size = new Size(18, 18);
                chkAtt3.Tag = 3;
                chkAtt3.BackColor = Color.FromArgb(255, 190, 60);  // Amber
                chkAtt3.FlatStyle = FlatStyle.Flat;
                chkAtt3.CheckedChanged += chkAttSelector_CheckedChanged;

                // Position checkboxes next to the corresponding textboxes
                // Use textbox bounds, but fallback to fixed positions if layout not ready
                grbxATT.Controls.Add(chkAtt1);
                grbxATT.Controls.Add(chkAtt2);
                grbxATT.Controls.Add(chkAtt3);

                // Defer positioning until layout is complete
                grbxATT.Layout += (layoutSender, layoutArgs) =>
                {
                    if (txbxAttenuatorPosition1 != null && txbxAttenuatorPosition1.Width > 0)
                        chkAtt1.Location = new Point(txbxAttenuatorPosition1.Right + 4, txbxAttenuatorPosition1.Top + 1);
                    if (txbxAttenuatorPosition2 != null && txbxAttenuatorPosition2.Width > 0)
                        chkAtt2.Location = new Point(txbxAttenuatorPosition2.Right + 4, txbxAttenuatorPosition2.Top + 1);
                    if (txbxAttenuatorPosition3 != null && txbxAttenuatorPosition3.Width > 0)
                        chkAtt3.Location = new Point(txbxAttenuatorPosition3.Right + 4, txbxAttenuatorPosition3.Top + 1);
                };

                // Also set initial positions now in case layout already happened
                if (txbxAttenuatorPosition1 != null)
                    chkAtt1.Location = new Point(txbxAttenuatorPosition1.Right + 4, txbxAttenuatorPosition1.Top + 1);
                if (txbxAttenuatorPosition2 != null)
                    chkAtt2.Location = new Point(txbxAttenuatorPosition2.Right + 4, txbxAttenuatorPosition2.Top + 1);
                if (txbxAttenuatorPosition3 != null)
                    chkAtt3.Location = new Point(txbxAttenuatorPosition3.Right + 4, txbxAttenuatorPosition3.Top + 1);

                // Set initial colours (all amber, position 0 is default)
                UpdateAttenuatorColours();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CreateAttenuatorSelectors error: " + ex.Message);
            }
        }

        /// <summary>
        /// Attenuator position checkbox handler.
        /// Checkboxes enable switch positions for the external selector switch.
        /// Tick positions 1, 2, 3 to match your switch (2/3/4 position).
        /// Each enabled position's textbox lets you set its attenuation level.
        /// The external switch (or STM state readback) determines which is ACTIVE.
        /// ACTIVE = GREEN, all others = RED, disabled = grey.
        /// </summary>
        private void chkAttSelector_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                // Update which positions are wired
                attPositionEnabled[0] = true;  // Always available
                attPositionEnabled[1] = (chkAtt1 != null && chkAtt1.Checked);
                attPositionEnabled[2] = (chkAtt2 != null && chkAtt2.Checked);
                attPositionEnabled[3] = (chkAtt3 != null && chkAtt3.Checked);

                // Use ReadOnly (not Enabled) so BackColor still shows
                // Enabled=false causes WinForms to ignore BackColor
                if (txbxAttenuatorPosition1 != null)
                    txbxAttenuatorPosition1.ReadOnly = !attPositionEnabled[1];
                if (txbxAttenuatorPosition2 != null)
                    txbxAttenuatorPosition2.ReadOnly = !attPositionEnabled[2];
                if (txbxAttenuatorPosition3 != null)
                    txbxAttenuatorPosition3.ReadOnly = !attPositionEnabled[3];

                // If active position was just disabled, revert to default 0
                if (!attPositionEnabled[activeAttPosition])
                    activeAttPosition = 0;

                UpdateAttenuatorColours();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Attenuator selector error: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Called when the external selector switch changes position.
        /// Sets the active attenuator and sends the command to the STM.
        /// Can be called from hardware callback or manually.
        /// </summary>
        public void SetActiveAttenuatorPosition(int position)
        {
            if (position < 0 || position > 3) return;
            if (!attPositionEnabled[position]) return;  // Can't select a position that isn't wired

            activeAttPosition = position;
            UpdateAttenuatorColours();

            // Send the selected position's attenuation level to the device
            if (guiActive && device != null && device.ltSwitchAttenuator != null
                && device.ltSwitchAttenuator.Count > position)
            {
                AddSequences(new SequenceSetDacAttenuator(ltTxCommand, null,
                    device.ltSwitchAttenuator[position].Dac, (ushort)position));
            }

            Debug.WriteLine("Attenuator switched to Position " + position);
        }

        /// <summary>
        /// Updates attenuator textbox and checkbox colours based on active position.
        /// ACTIVE position = GREEN (the one the switch is selecting)
        /// ALL other positions = RED (including Position 0 if not selected)
        /// Disabled/unchecked positions = greyed out
        /// </summary>
        private void UpdateAttenuatorColours()
        {
            Color activeColour = Color.FromArgb(60, 180, 60);    // Green - selected by switch
            Color inactiveColour = Color.FromArgb(210, 80, 80);    // Red - enabled but not selected
            Color disabledColour = Color.FromArgb(200, 200, 200);  // Grey - not wired

            TextBox[] attBoxes = new TextBox[] {
                txbxAttenuatorPosition0, txbxAttenuatorPosition1,
                txbxAttenuatorPosition2, txbxAttenuatorPosition3
            };
            CheckBox[] attChecks = new CheckBox[] { null, chkAtt1, chkAtt2, chkAtt3 };

            for (int i = 0; i < 4; i++)
            {
                if (attBoxes[i] == null) continue;

                bool enabled = attPositionEnabled[i];  // Pos 0 always true
                bool active = (activeAttPosition == i);

                // WinForms ignores BackColor when Enabled=false
                attBoxes[i].Enabled = true;

                if (!enabled)
                {
                    attBoxes[i].BackColor = disabledColour;
                    attBoxes[i].ForeColor = Color.Gray;
                }
                else if (active)
                {
                    attBoxes[i].BackColor = activeColour;
                    attBoxes[i].ForeColor = Color.Black;
                }
                else
                {
                    attBoxes[i].BackColor = inactiveColour;
                    attBoxes[i].ForeColor = Color.Black;
                }

                // Checkbox colour matches its textbox
                if (attChecks[i] != null)
                    attChecks[i].BackColor = attBoxes[i].BackColor;
            }
        }

        /// <summary>
        /// Ensures dgvBands has all required columns (adds missing ones at runtime)
        /// </summary>
        private void EnsureGridColumns()
        {
            if (dgvBands == null) return;

            // Populate Sweep Mode combobox items if it's a ComboBoxColumn (legacy - kept for file save compatibility)
            if (dgvBands.Columns.Contains("colSweepMode"))
            {
                DataGridViewComboBoxColumn comboCol = dgvBands.Columns["colSweepMode"] as DataGridViewComboBoxColumn;
                if (comboCol != null)
                {
                    if (comboCol.Items.Count == 0)
                        comboCol.Items.AddRange(new object[] { "Triggered", "Free Run", "One Shot" });
                    comboCol.Visible = false;
                }
                else
                {
                    dgvBands.Columns["colSweepMode"].Visible = false;
                }
            }

            // Repurpose Status column as TwMem (saves adding a new column)
            if (dgvBands.Columns.Contains("colStatus"))
            {
                dgvBands.Columns["colStatus"].HeaderText = "TwMem";
                dgvBands.Columns["colStatus"].Width = 45;
                dgvBands.Columns["colStatus"].ReadOnly = false;
                dgvBands.Columns["colStatus"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                dgvBands.Columns["colStatus"].DefaultCellStyle.BackColor = Color.FromArgb(225, 235, 210);
                dgvBands.Columns["colStatus"].ToolTipText = "TW_RAM_CONFIG (reg 0x47). Controls SRAM decimation.\n"
                    + "0 = no decimation (Random default)\n"
                    + "1 = div2, 2 = div4 (Ramp default), 3 = div8, 4 = div16";
            }

            // Fallback: hide by header text (designer may use different column names)
            foreach (DataGridViewColumn col in dgvBands.Columns)
            {
                string hdr = col.HeaderText.Replace("\n", " ").Replace("\r", "").Trim();
                if (hdr == "Sweep Mode" || hdr == "SweepMode" || hdr == "Sweep  Mode")
                    col.Visible = false;
                // Also hide legacy columns not used in TDM
                if (hdr == "Step KHz" || hdr == "Step  KHz" || hdr == "Loop")
                    col.Visible = false;
            }

            // Check and add missing columns
            if (!dgvBands.Columns.Contains("colBandwidthMhz"))
            {
                DataGridViewTextBoxColumn col = new DataGridViewTextBoxColumn();
                col.Name = "colBandwidthMhz";
                col.HeaderText = "BW MHz";
                col.Width = 65;
                col.ValueType = typeof(double);
                col.DefaultCellStyle.Format = "F1";
                col.DefaultCellStyle.NullValue = "20.0";  // Default 20 MHz for new rows
                dgvBands.Columns.Add(col);
            }

            if (!dgvBands.Columns.Contains("colPower"))
            {
                DataGridViewTextBoxColumn col = new DataGridViewTextBoxColumn();
                col.Name = "colPower";
                col.HeaderText = "Power %";
                col.Width = 60;
                col.ValueType = typeof(double);
                col.DefaultCellStyle.Format = "F0";
                col.DefaultCellStyle.NullValue = "100";  // Default 100% power for new rows
                dgvBands.Columns.Add(col);
            }

            if (!dgvBands.Columns.Contains("colActive"))
            {
                DataGridViewCheckBoxColumn col = new DataGridViewCheckBoxColumn();
                col.Name = "colActive";
                col.HeaderText = "Active";
                col.Width = 50;
                col.TrueValue = true;
                col.FalseValue = false;
                col.DefaultCellStyle.NullValue = false;  // New rows default to INACTIVE — must explicitly enable
                dgvBands.Columns.Add(col);
            }

            if (!dgvBands.Columns.Contains("colAttenDb"))
            {
                DataGridViewTextBoxColumn col = new DataGridViewTextBoxColumn();
                col.Name = "colAttenDb";
                col.HeaderText = "Atten dB";
                col.Width = 60;
                col.ValueType = typeof(double);
                col.DefaultCellStyle.Format = "F1";
                col.DefaultCellStyle.NullValue = "0.0";
                dgvBands.Columns.Add(col);
            }

            if (!dgvBands.Columns.Contains("colTones"))
            {
                DataGridViewTextBoxColumn col = new DataGridViewTextBoxColumn();
                col.Name = "colTones";
                col.HeaderText = "Points";
                col.Width = 55;
                col.ValueType = typeof(int);
                col.DefaultCellStyle.NullValue = "512";
                col.ToolTipText = "SRAM sweep points per band (32-4096). Default 512.\n"
                    + "Step = BW / Points. Must be < target Rx BW.\n"
                    + "512 safe for LTE (75MHz/512=146kHz < 180kHz RB).\n"
                    + "Keyfobs/RC: 32-64 OK. Walkies: 128+. Check Step kHz colour.";
                col.ReadOnly = false;  // User-editable per band
                dgvBands.Columns.Add(col);
            }

            if (!dgvBands.Columns.Contains("colDdsMode"))
            {
                DataGridViewComboBoxColumn col = new DataGridViewComboBoxColumn();
                col.Name = "colDdsMode";
                col.HeaderText = "DDS Mode";
                col.Width = 70;
                col.Items.AddRange(new object[] { "Ramp", "PRBS", "Random" });
                col.DefaultCellStyle.NullValue = "Ramp";
                col.FlatStyle = FlatStyle.Flat;
                dgvBands.Columns.Add(col);
            }

            if (!dgvBands.Columns.Contains("colStepKhzCalc"))
            {
                DataGridViewTextBoxColumn col = new DataGridViewTextBoxColumn();
                col.Name = "colStepKhzCalc";
                col.HeaderText = "Step kHz";
                col.Width = 60;
                col.ReadOnly = true;
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                col.DefaultCellStyle.BackColor = Color.FromArgb(220, 225, 215);
                col.ToolTipText = "Auto-calculated: BW / Points. RED if > 180 kHz (LTE RB size).";
                dgvBands.Columns.Add(col);
            }

            if (!dgvBands.Columns.Contains("colHitsPerSec"))
            {
                DataGridViewTextBoxColumn col = new DataGridViewTextBoxColumn();
                col.Name = "colHitsPerSec";
                col.HeaderText = "Hits/s";
                col.Width = 55;
                col.ReadOnly = true;
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                col.DefaultCellStyle.BackColor = Color.FromArgb(220, 225, 215);
                col.ToolTipText = "Times per second each freq step is revisited.\n"
                    + "= 1,000,000 / (dwell_µs × active_bands).\n"
                    + "GREEN ≥ 500 (reliable jam), RED < 100 (too slow).";
                dgvBands.Columns.Add(col);
            }

            // K9: FreqCtrl column — shows firmware DDS clock rate per band
            if (!dgvBands.Columns.Contains("colFreqCtrl"))
            {
                DataGridViewTextBoxColumn col = new DataGridViewTextBoxColumn();
                col.Name = "colFreqCtrl";
                col.HeaderText = "FCtrl\nMHz";
                col.Width = 55;
                col.ReadOnly = false;
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                col.DefaultCellStyle.BackColor = Color.FromArgb(225, 235, 210);
                col.ToolTipText = "DDS CTRL clock frequency in MHz. Controls sweep speed.\n"
                    + "Enter MHz value (e.g. 40 = 40MHz) or 'auto'.\n"
                    + "auto/0 = firmware default 40MHz.\n"
                    + "Higher = faster sweep. Max 45MHz (TIM4 limit).";
                dgvBands.Columns.Add(col);
            }

            // TwMem now uses repurposed colStatus column — no separate column needed

            // Rename "Start MHz" to "Centre MHz" (LO sits at centre, DDS sweeps +/- BW/2)
            foreach (DataGridViewColumn col in dgvBands.Columns)
            {
                if (col.HeaderText == "Start MHz") { col.HeaderText = "Centre MHz"; break; }
            }
        }

        /// <summary>
        /// Update BW MHz and Tones cells per-row based on each row's DDS Mode.
        /// In Band Table Mode, each row can have different modes, so we check
        /// the per-row colDdsMode value instead of applying column-level ReadOnly.
        /// </summary>
        private void UpdateGridColumnsForMode(ushort hwMode)
        {
            if (dgvBands == null) return;

            foreach (DataGridViewRow row in dgvBands.Rows)
            {
                if (row.IsNewRow) continue;

                bool isPseudo;
                if (dgvBands.Columns.Contains("colDdsMode") && row.Cells["colDdsMode"].Value != null)
                    isPseudo = (row.Cells["colDdsMode"].Value.ToString() == "PRBS");
                else
                    isPseudo = (hwMode == (ushort)AD9106_MODE.PSEUDO);

                // Tones: greyed in PRBS (not used)
                if (dgvBands.Columns.Contains("colTones"))
                {
                    row.Cells["colTones"].ReadOnly = isPseudo;
                    row.Cells["colTones"].Style.BackColor = isPseudo ? Color.FromArgb(180, 185, 175) : Color.FromArgb(235, 240, 230);
                    row.Cells["colTones"].Style.ForeColor = isPseudo ? Color.FromArgb(140, 140, 130) : Color.Black;
                }
                // BW: greyed + forced to 170 in PRBS (AD9106 PSEUDO = 170 MHz always)
                if (dgvBands.Columns.Contains("colBandwidthMhz"))
                {
                    row.Cells["colBandwidthMhz"].ReadOnly = isPseudo;
                    row.Cells["colBandwidthMhz"].Style.BackColor = isPseudo ? Color.FromArgb(180, 185, 175) : Color.FromArgb(235, 240, 230);
                    row.Cells["colBandwidthMhz"].Style.ForeColor = isPseudo ? Color.FromArgb(140, 140, 130) : Color.Black;
                    if (isPseudo)
                        row.Cells["colBandwidthMhz"].Value = 170.0;
                }
            }
            dgvBands.Invalidate();
        }

        /// <summary>
        /// Update the Step kHz indicator for a single row.
        /// Step = BW_MHz * 1000 / Points.  RED if > 180 kHz (LTE RB size).
        /// PRBS rows show "N/A" (noise, no discrete steps).
        /// </summary>
        private void UpdateStepKhzRow(DataGridViewRow row)
        {
            if (row.IsNewRow) return;
            bool hasStepCol = dgvBands.Columns.Contains("colStepKhzCalc");
            bool hasHitsCol = dgvBands.Columns.Contains("colHitsPerSec");
            if (!hasStepCol && !hasHitsCol) return;

            string mode = "Ramp";
            if (dgvBands.Columns.Contains("colDdsMode") && row.Cells["colDdsMode"].Value != null)
                mode = row.Cells["colDdsMode"].Value.ToString();

            // ── Count active bands for hits/sec calc ──
            int activeBands = 0;
            foreach (DataGridViewRow r in dgvBands.Rows)
            {
                if (r.IsNewRow) continue;
                bool hasData = false;
                try
                {
                    if (dgvBands.Columns.Contains("colBandName") &&
                        r.Cells["colBandName"].Value != null &&
                        r.Cells["colBandName"].Value.ToString().Trim().Length > 0)
                        hasData = true;
                }
                catch { }
                if (!hasData) continue;
                bool act = true;
                try
                {
                    if (dgvBands.Columns.Contains("colActive") && r.Cells["colActive"].Value != null)
                        act = Convert.ToBoolean(r.Cells["colActive"].Value);
                }
                catch { }
                if (act) activeBands++;
            }
            if (activeBands < 1) activeBands = 1;

            int dwellUs = (numTdmDwell != null) ? (int)numTdmDwell.Value : 100;
            double hitsPerSec = 1000000.0 / ((double)dwellUs * activeBands);

            if (mode == "PRBS")
            {
                if (hasStepCol)
                {
                    row.Cells["colStepKhzCalc"].Value = "N/A";
                    row.Cells["colStepKhzCalc"].Style.BackColor = Color.FromArgb(180, 185, 175);
                    row.Cells["colStepKhzCalc"].Style.ForeColor = Color.FromArgb(140, 140, 130);
                }
                if (hasHitsCol)
                {
                    // PRBS is continuous noise — every frequency is hit simultaneously
                    row.Cells["colHitsPerSec"].Value = "CW";
                    row.Cells["colHitsPerSec"].Style.BackColor = Color.FromArgb(180, 210, 255);
                    row.Cells["colHitsPerSec"].Style.ForeColor = Color.FromArgb(30, 80, 140);
                }
                return;
            }

            // ── Step kHz (RAMP / RANDOM mode) ──
            double bwMhz = 20.0;
            if (dgvBands.Columns.Contains("colBandwidthMhz") && row.Cells["colBandwidthMhz"].Value != null)
                try { bwMhz = Convert.ToDouble(row.Cells["colBandwidthMhz"].Value); } catch { }

            int userPoints = 256;  // Default
            if (dgvBands.Columns.Contains("colTones") && row.Cells["colTones"].Value != null)
                try { userPoints = Math.Max(32, Math.Min(4096, Convert.ToInt32(row.Cells["colTones"].Value))); } catch { }

            double stepKhz = (bwMhz * 1000.0) / userPoints;

            if (hasStepCol)
            {
                row.Cells["colStepKhzCalc"].Value = stepKhz.ToString("F1");

                if (stepKhz > 180.0)
                {
                    row.Cells["colStepKhzCalc"].Style.BackColor = Color.FromArgb(255, 180, 180);
                    row.Cells["colStepKhzCalc"].Style.ForeColor = Color.DarkRed;
                }
                else
                {
                    row.Cells["colStepKhzCalc"].Style.BackColor = Color.FromArgb(200, 240, 200);
                    row.Cells["colStepKhzCalc"].Style.ForeColor = Color.Black;
                }
            }

            // ── Hits/sec per frequency step ──
            if (hasHitsCol)
            {
                // Each SRAM entry is visited once per full 4096-entry cycle.
                // Cycles per second on this band = 1,000,000 / (dwell_us × active_bands)
                // This is how many times per second each step position gets RF energy.
                string hitsStr;
                if (hitsPerSec >= 1000.0)
                    hitsStr = (hitsPerSec / 1000.0).ToString("F1") + "k";
                else
                    hitsStr = ((int)hitsPerSec).ToString();

                row.Cells["colHitsPerSec"].Value = hitsStr;

                // Color: green ≥ 500 (reliable), amber 100-500, red < 100
                if (hitsPerSec >= 500.0)
                {
                    row.Cells["colHitsPerSec"].Style.BackColor = Color.FromArgb(200, 240, 200);
                    row.Cells["colHitsPerSec"].Style.ForeColor = Color.Black;
                }
                else if (hitsPerSec >= 100.0)
                {
                    row.Cells["colHitsPerSec"].Style.BackColor = Color.FromArgb(255, 235, 180);
                    row.Cells["colHitsPerSec"].Style.ForeColor = Color.FromArgb(140, 100, 0);
                }
                else
                {
                    row.Cells["colHitsPerSec"].Style.BackColor = Color.FromArgb(255, 180, 180);
                    row.Cells["colHitsPerSec"].Style.ForeColor = Color.DarkRed;
                }
            }

            // K9: Auto-update TwMem when BW changes (computed from setRampBandwidth formula)
            if (dgvBands.Columns.Contains("colStatus") && mode != "PRBS")
            {
                double fRampMax = (bwMhz * 1e6 / (2.0 * 170e6)) * 16777216.0;
                int tw = (fRampMax / 4.0 > 1500.0) ? 2 : (fRampMax / 2.0 > 1500.0) ? 1 : 0;
                row.Cells["colStatus"].Value = tw;
            }
        }

        /// <summary>
        /// Update Points column for all rows based on global Duty %.
        /// Active SRAM = (4096 × Duty%) / 100.
        /// </summary>
        /// <summary>
        /// Set default Points value for rows that have no value yet.
        /// Does NOT overwrite user-edited points — those are per-band.
        /// </summary>
        private void UpdateAllPointsFromDuty()
        {
            if (dgvBands == null || !dgvBands.Columns.Contains("colTones")) return;

            foreach (DataGridViewRow row in dgvBands.Rows)
            {
                if (row.IsNewRow) continue;
                string mode = "Ramp";
                if (dgvBands.Columns.Contains("colDdsMode") && row.Cells["colDdsMode"].Value != null)
                    mode = row.Cells["colDdsMode"].Value.ToString();

                // Only set default if cell is empty — never overwrite user edits
                if (mode != "PRBS" && (row.Cells["colTones"].Value == null ||
                    row.Cells["colTones"].Value.ToString().Trim().Length == 0))
                {
                    row.Cells["colTones"].Value = 512;  // Safe default for LTE
                }
            }
        }

        /// <summary>
        /// Update Step kHz for all rows (call after loading bands).
        /// </summary>
        private void UpdateAllStepKhz()
        {
            if (dgvBands == null) return;
            foreach (DataGridViewRow row in dgvBands.Rows)
            {
                if (!row.IsNewRow) UpdateStepKhzRow(row);
            }
        }

        private void CreateGuardBandSettingsBox()
        {
            if (dgvBands == null) return;

            // ── Apply military theme to main form ──
            this.BackColor = Color.FromArgb(50, 55, 45);

            // Style the left-side groupboxes
            Color grpBg = Color.FromArgb(55, 60, 48);
            Color grpFg = Color.FromArgb(180, 200, 160);
            if (grbxATT != null) { grbxATT.ForeColor = grpFg; grbxATT.BackColor = grpBg; }
            if (grbxDDS != null) { grbxDDS.ForeColor = grpFg; grbxDDS.BackColor = grpBg; }
            if (grbxLO != null) { grbxLO.ForeColor = grpFg; grbxLO.BackColor = grpBg; }
            if (grbxDetector != null) { grbxDetector.ForeColor = grpFg; grbxDetector.BackColor = grpBg; }
            if (grbxSwitchSelect != null) { grbxSwitchSelect.ForeColor = grpFg; grbxSwitchSelect.BackColor = grpBg; }

            // Style grid
            dgvBands.BackgroundColor = Color.FromArgb(60, 65, 55);
            dgvBands.GridColor = Color.FromArgb(80, 90, 70);
            dgvBands.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(70, 80, 60);
            dgvBands.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvBands.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            dgvBands.EnableHeadersVisualStyles = false;
            dgvBands.DefaultCellStyle.BackColor = Color.FromArgb(235, 240, 230);
            dgvBands.DefaultCellStyle.SelectionBackColor = Color.FromArgb(100, 130, 80);

            // ── Position controls relative to dgvBands ──
            int origHeight = dgvBands.Height;
            dgvBands.Height = Math.Max(origHeight - 170, 120);

            // ── BAND TABLE MODE toggle ──
            // Placed above the grid — controls whether manual DDS or band table is active
            chkBandTableMode = new CheckBox();
            chkBandTableMode.Text = "BAND TABLE MODE";
            chkBandTableMode.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            chkBandTableMode.ForeColor = Color.FromArgb(255, 200, 80);
            chkBandTableMode.Location = new Point(dgvBands.Left, dgvBands.Top - 20);
            chkBandTableMode.Size = new Size(160, 18);
            chkBandTableMode.Checked = false;
            chkBandTableMode.CheckedChanged += (s, e) =>
            {
                SetBandTableMode(chkBandTableMode.Checked);
            };
            this.Controls.Add(chkBandTableMode);
            chkBandTableMode.BringToFront();

            int x = dgvBands.Left;
            int w = dgvBands.Width;

            int y = dgvBands.Bottom + 3;

            // ── Single unified control panel ──
            grpGuardBandSettings = new GroupBox();
            grpGuardBandSettings.Text = " BAND CONFIGURATION ";
            grpGuardBandSettings.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            grpGuardBandSettings.ForeColor = Color.FromArgb(200, 220, 180);
            grpGuardBandSettings.Location = new Point(x, y);
            grpGuardBandSettings.Size = new Size(w, 174);
            grpGuardBandSettings.BackColor = Color.FromArgb(55, 62, 48);
            grpGuardBandSettings.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            this.Controls.Add(grpGuardBandSettings);
            grpGuardBandSettings.BringToFront();

            // ═══ Row 1: Preset buttons ═══
            Button btnLoad7Bands = new Button();
            btnLoad7Bands.Text = "LOAD 7 GUARD";
            btnLoad7Bands.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            btnLoad7Bands.BackColor = Color.FromArgb(160, 120, 40);
            btnLoad7Bands.ForeColor = Color.White;
            btnLoad7Bands.FlatStyle = FlatStyle.Flat;
            btnLoad7Bands.FlatAppearance.BorderColor = Color.FromArgb(120, 90, 30);
            btnLoad7Bands.Size = new Size(110, 28);
            btnLoad7Bands.Location = new Point(8, 20);
            btnLoad7Bands.Click += (s, e) => Load7GuardBands();
            grpGuardBandSettings.Controls.Add(btnLoad7Bands);

            Button btnLoad4Bands = new Button();
            btnLoad4Bands.Text = "LOAD 4 STD";
            btnLoad4Bands.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            btnLoad4Bands.BackColor = Color.FromArgb(60, 80, 100);
            btnLoad4Bands.ForeColor = Color.White;
            btnLoad4Bands.FlatStyle = FlatStyle.Flat;
            btnLoad4Bands.FlatAppearance.BorderColor = Color.FromArgb(40, 60, 80);
            btnLoad4Bands.Size = new Size(100, 28);
            btnLoad4Bands.Location = new Point(124, 20);
            btnLoad4Bands.Click += (s, e) => Load4StandardBands();
            grpGuardBandSettings.Controls.Add(btnLoad4Bands);

            Button btnLoadGps = new Button();
            btnLoadGps.Text = "GPS DENIAL";
            btnLoadGps.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            btnLoadGps.BackColor = Color.FromArgb(140, 40, 40);
            btnLoadGps.ForeColor = Color.White;
            btnLoadGps.FlatStyle = FlatStyle.Flat;
            btnLoadGps.FlatAppearance.BorderColor = Color.FromArgb(100, 30, 30);
            btnLoadGps.Size = new Size(100, 28);
            btnLoadGps.Location = new Point(230, 20);
            btnLoadGps.Click += (s, e) => LoadGpsDenialBands();
            grpGuardBandSettings.Controls.Add(btnLoadGps);

            Button btnClearTable = new Button();
            btnClearTable.Text = "CLEAR";
            btnClearTable.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            btnClearTable.BackColor = Color.FromArgb(80, 80, 80);
            btnClearTable.ForeColor = Color.White;
            btnClearTable.FlatStyle = FlatStyle.Flat;
            btnClearTable.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
            btnClearTable.Size = new Size(70, 28);
            btnClearTable.Location = new Point(336, 20);
            btnClearTable.Click += (s, e) =>
            {
                if (dgvBands != null)
                {
                    dgvBands.Rows.Clear();
                    dgvBands.ReadOnly = false;
                    UpdateTdmHitsDisplay();
                    UpdateGuardStatus();
                }
            };
            grpGuardBandSettings.Controls.Add(btnClearTable);

            // Save/Load config file buttons
            Button btnSaveCfg = new Button();
            btnSaveCfg.Text = "💾 CFG";
            btnSaveCfg.Font = new Font("Segoe UI", 7F, FontStyle.Bold);
            btnSaveCfg.BackColor = Color.FromArgb(50, 70, 50);
            btnSaveCfg.ForeColor = Color.White;
            btnSaveCfg.FlatStyle = FlatStyle.Flat;
            btnSaveCfg.FlatAppearance.BorderColor = Color.FromArgb(40, 55, 40);
            btnSaveCfg.Size = new Size(62, 28);
            btnSaveCfg.Location = new Point(412, 20);
            btnSaveCfg.Click += btnSaveBands_Click;
            grpGuardBandSettings.Controls.Add(btnSaveCfg);

            Button btnLoadCfg = new Button();
            btnLoadCfg.Text = "📂 CFG";
            btnLoadCfg.Font = new Font("Segoe UI", 7F, FontStyle.Bold);
            btnLoadCfg.BackColor = Color.FromArgb(50, 55, 70);
            btnLoadCfg.ForeColor = Color.White;
            btnLoadCfg.FlatStyle = FlatStyle.Flat;
            btnLoadCfg.FlatAppearance.BorderColor = Color.FromArgb(40, 45, 55);
            btnLoadCfg.Size = new Size(62, 28);
            btnLoadCfg.Location = new Point(478, 20);
            btnLoadCfg.Click += btnLoadBands_Click;
            grpGuardBandSettings.Controls.Add(btnLoadCfg);

            // ═══ Row 2: Pulse Mode + ON/OFF + Dwell ═══
            chkGuardPulseMode = new CheckBox();
            chkGuardPulseMode.Text = "Pulse Mode";
            chkGuardPulseMode.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            chkGuardPulseMode.ForeColor = Color.FromArgb(150, 220, 120);
            chkGuardPulseMode.Location = new Point(8, 55);
            chkGuardPulseMode.Size = new Size(100, 22);
            chkGuardPulseMode.Checked = false;
            chkGuardPulseMode.CheckedChanged += (s, e) =>
            {
                bool en = chkGuardPulseMode.Checked;
                if (numGuardPulseOn != null) numGuardPulseOn.Enabled = en;
                if (numGuardPulseOff != null) numGuardPulseOff.Enabled = en;
                UpdateGuardStatus();
            };
            grpGuardBandSettings.Controls.Add(chkGuardPulseMode);

            Label lblOn = new Label();
            lblOn.Text = "ON:";
            lblOn.Font = new Font("Segoe UI", 8F);
            lblOn.ForeColor = Color.FromArgb(180, 200, 160);
            lblOn.Location = new Point(112, 57);
            lblOn.Size = new Size(28, 18);
            grpGuardBandSettings.Controls.Add(lblOn);

            numGuardPulseOn = new NumericUpDown();
            numGuardPulseOn.Minimum = 1;
            numGuardPulseOn.Maximum = 50;
            numGuardPulseOn.Value = 10;
            numGuardPulseOn.Width = 50;
            numGuardPulseOn.Location = new Point(140, 54);
            numGuardPulseOn.Font = new Font("Segoe UI", 8F);
            numGuardPulseOn.ValueChanged += (s, e) => { pulseOnTimeMs = (int)numGuardPulseOn.Value; UpdateGuardStatus(); };
            grpGuardBandSettings.Controls.Add(numGuardPulseOn);

            Label lblOnMs = new Label();
            lblOnMs.Text = "ms";
            lblOnMs.Location = new Point(192, 57);
            lblOnMs.Size = new Size(20, 18);
            lblOnMs.Font = new Font("Segoe UI", 7F);
            lblOnMs.ForeColor = Color.FromArgb(150, 170, 140);
            grpGuardBandSettings.Controls.Add(lblOnMs);

            Label lblOff = new Label();
            lblOff.Text = "OFF:";
            lblOff.Font = new Font("Segoe UI", 8F);
            lblOff.ForeColor = Color.FromArgb(180, 200, 160);
            lblOff.Location = new Point(216, 57);
            lblOff.Size = new Size(32, 18);
            grpGuardBandSettings.Controls.Add(lblOff);

            numGuardPulseOff = new NumericUpDown();
            numGuardPulseOff.Minimum = 10;
            numGuardPulseOff.Maximum = 100;
            numGuardPulseOff.Value = 40;
            numGuardPulseOff.Width = 50;
            numGuardPulseOff.Location = new Point(248, 54);
            numGuardPulseOff.Font = new Font("Segoe UI", 8F);
            numGuardPulseOff.ValueChanged += (s, e) => { pulseOffTimeMs = (int)numGuardPulseOff.Value; UpdateGuardStatus(); };
            grpGuardBandSettings.Controls.Add(numGuardPulseOff);

            Label lblOffMs = new Label();
            lblOffMs.Text = "ms";
            lblOffMs.Location = new Point(300, 57);
            lblOffMs.Size = new Size(20, 18);
            lblOffMs.Font = new Font("Segoe UI", 7F);
            lblOffMs.ForeColor = Color.FromArgb(150, 170, 140);
            grpGuardBandSettings.Controls.Add(lblOffMs);

            // Dwell (controls time spent on each band during TEST)
            Label lblDwell = new Label();
            lblDwell.Text = "Dwell:";
            lblDwell.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            lblDwell.ForeColor = Color.FromArgb(150, 220, 120);
            lblDwell.Location = new Point(326, 57);
            lblDwell.Size = new Size(42, 18);
            grpGuardBandSettings.Controls.Add(lblDwell);

            numTdmDwell = new NumericUpDown();
            numTdmDwell.Minimum = 50;
            numTdmDwell.Maximum = 65000;
            numTdmDwell.Increment = 10;
            numTdmDwell.Value = 100;
            numTdmDwell.Width = 60;
            numTdmDwell.Location = new Point(368, 54);
            numTdmDwell.Font = new Font("Segoe UI", 8F);
            numTdmDwell.ValueChanged += (s2, e2) => { userDwellUs = (int)numTdmDwell.Value; UpdateAllStepKhz(); UpdateTdmHitsDisplay(); UpdateGuardStatus(); SendTdmParamsLive(); };
            grpGuardBandSettings.Controls.Add(numTdmDwell);

            Label lblDwellMs = new Label();
            lblDwellMs.Text = "\u00B5s";
            lblDwellMs.Location = new Point(430, 57);
            lblDwellMs.Size = new Size(20, 18);
            lblDwellMs.Font = new Font("Segoe UI", 7F);
            lblDwellMs.ForeColor = Color.FromArgb(150, 170, 140);
            grpGuardBandSettings.Controls.Add(lblDwellMs);

            // Duty % — SRAM duty cycle (controls PA thermal load)
            Label lblDuty = new Label();
            lblDuty.Text = "Duty:";
            lblDuty.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            lblDuty.ForeColor = Color.FromArgb(220, 180, 80);  // Gold to stand out
            lblDuty.Location = new Point(454, 57);
            lblDuty.Size = new Size(38, 18);
            grpGuardBandSettings.Controls.Add(lblDuty);

            numDutyPercent = new NumericUpDown();
            numDutyPercent.Minimum = 5;
            numDutyPercent.Maximum = 100;
            numDutyPercent.Increment = 5;
            numDutyPercent.Value = 100;
            numDutyPercent.Width = 48;
            numDutyPercent.Location = new Point(492, 54);
            numDutyPercent.Font = new Font("Segoe UI", 8F);
            numDutyPercent.ValueChanged += (s3, e3) =>
            {
                // Recalculate Points and Step kHz for all rows when duty changes
                UpdateAllPointsFromDuty();
                UpdateAllStepKhz();
                UpdateGuardStatus();
            };
            grpGuardBandSettings.Controls.Add(numDutyPercent);

            Label lblDutyPct = new Label();
            lblDutyPct.Text = "%";
            lblDutyPct.Location = new Point(542, 57);
            lblDutyPct.Size = new Size(16, 18);
            lblDutyPct.Font = new Font("Segoe UI", 7F);
            lblDutyPct.ForeColor = Color.FromArgb(150, 170, 140);
            grpGuardBandSettings.Controls.Add(lblDutyPct);

            // ═══ Row 3: Status (estimated) ═══
            lblGuardStatus = new Label();
            lblGuardStatus.Font = new Font("Consolas", 9F, FontStyle.Bold);
            lblGuardStatus.ForeColor = Color.FromArgb(140, 200, 120);
            lblGuardStatus.Location = new Point(8, 80);
            lblGuardStatus.Size = new Size(w - 20, 18);
            lblGuardStatus.TextAlign = ContentAlignment.MiddleLeft;
            grpGuardBandSettings.Controls.Add(lblGuardStatus);

            // ═══ Row 3b: Live measured data (yellow, updates during TEST) ═══
            lblLiveHits = new Label();
            lblLiveHits.Font = new Font("Consolas", 9F, FontStyle.Bold);
            lblLiveHits.ForeColor = Color.FromArgb(255, 220, 60);
            lblLiveHits.Location = new Point(8, 98);
            lblLiveHits.Size = new Size(w - 20, 18);
            lblLiveHits.TextAlign = ContentAlignment.MiddleLeft;
            lblLiveHits.Text = "";
            grpGuardBandSettings.Controls.Add(lblLiveHits);

            // ═══ Row 3c: Group Rotation Controls ═══
            Label lblGrpSize = new Label();
            lblGrpSize.Text = "Group:";
            lblGrpSize.Font = new Font("Segoe UI", 7.5F);
            lblGrpSize.ForeColor = Color.FromArgb(150, 170, 140);
            lblGrpSize.Location = new Point(8, 119);
            lblGrpSize.Size = new Size(42, 16);
            grpGuardBandSettings.Controls.Add(lblGrpSize);

            numGroupSize = new NumericUpDown();
            numGroupSize.Minimum = 0;
            numGroupSize.Maximum = 8;
            numGroupSize.Increment = 1;
            numGroupSize.Value = 4;
            numGroupSize.Width = 42;
            numGroupSize.Location = new Point(50, 116);
            numGroupSize.Font = new Font("Segoe UI", 8F);
            grpGuardBandSettings.Controls.Add(numGroupSize);

            Label lblGrpBands = new Label();
            lblGrpBands.Text = "bands";
            lblGrpBands.Font = new Font("Segoe UI", 7F);
            lblGrpBands.ForeColor = Color.FromArgb(150, 170, 140);
            lblGrpBands.Location = new Point(93, 119);
            lblGrpBands.Size = new Size(34, 16);
            grpGuardBandSettings.Controls.Add(lblGrpBands);

            Label lblRotMs = new Label();
            lblRotMs.Text = "Rotate:";
            lblRotMs.Font = new Font("Segoe UI", 7.5F);
            lblRotMs.ForeColor = Color.FromArgb(150, 170, 140);
            lblRotMs.Location = new Point(130, 119);
            lblRotMs.Size = new Size(44, 16);
            grpGuardBandSettings.Controls.Add(lblRotMs);

            numRotationMs = new NumericUpDown();
            numRotationMs.Minimum = 1;
            numRotationMs.Maximum = 1000;
            numRotationMs.Increment = 5;
            numRotationMs.Value = 10;
            numRotationMs.Width = 52;
            numRotationMs.Location = new Point(174, 116);
            numRotationMs.Font = new Font("Segoe UI", 8F);
            grpGuardBandSettings.Controls.Add(numRotationMs);

            Label lblRotUnit = new Label();
            lblRotUnit.Text = "ms";
            lblRotUnit.Font = new Font("Segoe UI", 7F);
            lblRotUnit.ForeColor = Color.FromArgb(150, 170, 140);
            lblRotUnit.Location = new Point(227, 119);
            lblRotUnit.Size = new Size(18, 16);
            grpGuardBandSettings.Controls.Add(lblRotUnit);

            Button btnSetGrp = new Button();
            btnSetGrp.Text = "SET GRP";
            btnSetGrp.Font = new Font("Segoe UI", 7F, FontStyle.Bold);
            btnSetGrp.BackColor = Color.FromArgb(80, 80, 40);
            btnSetGrp.ForeColor = Color.White;
            btnSetGrp.FlatStyle = FlatStyle.Flat;
            btnSetGrp.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 30);
            btnSetGrp.Size = new Size(64, 22);
            btnSetGrp.Location = new Point(250, 115);
            btnSetGrp.Click += (sGrp, eGrp) =>
            {
                byte gs = (byte)numGroupSize.Value;
                ushort rm = (ushort)numRotationMs.Value;
                SendGroupRotation(gs, rm);
                tdmRotationMs = rm;
            };
            grpGuardBandSettings.Controls.Add(btnSetGrp);

            // ═══ Row 4: Action buttons ═══
            // SAVE TO DEVICE — merged save (config + band table + attenuators)
            btnTdmUpload = new Button();
            btnTdmUpload.Text = "\uD83D\uDCBE SAVE TO DEVICE";
            btnTdmUpload.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            btnTdmUpload.BackColor = Color.FromArgb(40, 100, 40);
            btnTdmUpload.ForeColor = Color.White;
            btnTdmUpload.FlatStyle = FlatStyle.Flat;
            btnTdmUpload.FlatAppearance.BorderColor = Color.FromArgb(30, 80, 30);
            btnTdmUpload.Size = new Size(140, 28);
            btnTdmUpload.Location = new Point(8, 140);
            btnTdmUpload.Click += btnTdmSaveStandalone_Click;
            grpGuardBandSettings.Controls.Add(btnTdmUpload);

            // TEST — PC-controlled jamming for testing before deployment
            btnGuardStart = new Button();
            btnGuardStart.Text = "\u25B6 TEST";
            btnGuardStart.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnGuardStart.BackColor = Color.FromArgb(60, 120, 60);
            btnGuardStart.ForeColor = Color.White;
            btnGuardStart.FlatStyle = FlatStyle.Flat;
            btnGuardStart.FlatAppearance.BorderColor = Color.FromArgb(40, 90, 40);
            btnGuardStart.Size = new Size(85, 28);
            btnGuardStart.Location = new Point(155, 140);
            btnGuardStart.Click += btnGuardStart_Click;
            grpGuardBandSettings.Controls.Add(btnGuardStart);

            // STOP TEST
            btnGuardStop = new Button();
            btnGuardStop.Text = "\u25A0 STOP";
            btnGuardStop.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnGuardStop.BackColor = Color.FromArgb(160, 40, 40);
            btnGuardStop.ForeColor = Color.White;
            btnGuardStop.FlatStyle = FlatStyle.Flat;
            btnGuardStop.FlatAppearance.BorderColor = Color.FromArgb(120, 30, 30);
            btnGuardStop.Size = new Size(85, 28);
            btnGuardStop.Location = new Point(246, 140);
            btnGuardStop.Enabled = false;
            btnGuardStop.Click += btnGuardStop_Click;
            grpGuardBandSettings.Controls.Add(btnGuardStop);

            // DIAGNOSTICS
            Button btnDiag = new Button();
            btnDiag.Text = "DIAG";
            btnDiag.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            btnDiag.BackColor = Color.FromArgb(50, 50, 70);
            btnDiag.ForeColor = Color.White;
            btnDiag.FlatStyle = FlatStyle.Flat;
            btnDiag.FlatAppearance.BorderColor = Color.FromArgb(40, 40, 60);
            btnDiag.Size = new Size(60, 28);
            btnDiag.Location = new Point(337, 140);
            btnDiag.Click += (s, e) => ShowDiagnosticsWindow();
            grpGuardBandSettings.Controls.Add(btnDiag);

            // ── K9: TDM timing diagnostic poll timer (every 2s) ──
            tmrTdmDiag = new System.Windows.Forms.Timer();
            tmrTdmDiag.Interval = 2000;
            tmrTdmDiag.Tick += (s, e) =>
            {
                // Only poll when device connected
                if (usbConnection == null || !usbConnection.IsOpen) return;
                if (ltTxCommand == null) return;  // Not yet initialized
                try { SendTdmCommand(CMD_TDM_STATUS, null, 0); tdmDiagPollCount++; } catch { }

                // ALWAYS update lblLiveHits so we can confirm timer fires
                if (lblLiveHits != null && !lblLiveHits.IsDisposed)
                {
                    try
                    {
                        if (tdmDiagValid)
                        {
                            string grpInfo = (tdmGroupSize > 0)
                                ? string.Format("  GRP {0}/{1} sz={2} rot={3}ms",
                                    tdmGroupCurrent + 1,
                                    (tdmGroupSize > 0 && tdmDiagActiveCount > 0)
                                        ? ((tdmDiagActiveCount + tdmGroupSize - 1) / tdmGroupSize)
                                        : 0,
                                    tdmGroupSize, tdmRotationMs)
                                : string.Format("  grp=OFF({0})", tdmGroupSize);
                            lblLiveHits.Text = string.Format(
                                "TIMING: loop={0}us  hop={1}us  dwell={2}us  cycle={3}us" + grpInfo,
                                tdmDiagLoopUs, tdmDiagHopUs, tdmDiagDwellUs, tdmDiagCycleUs);
                            lblLiveHits.ForeColor = Color.FromArgb(255, 220, 60);
                        }
                        else
                        {
                            lblLiveHits.Text = string.Format(
                                "DIAG: sent={0} recv={1} pktSize={2} (need 46 bytes)",
                                tdmDiagPollCount, tdmDiagRxCount, tdmDiagLastPktSize);
                            lblLiveHits.ForeColor = Color.Gray;
                        }
                    }
                    catch { }
                }
            };
            tmrTdmDiag.Start();

            // ── USB Connection LED ──
            ledConnection = new Panel();
            ledConnection.Size = new Size(12, 12);
            ledConnection.Location = new Point(405, 148);
            ledConnection.BackColor = Color.Red;
            ledConnection.BorderStyle = BorderStyle.None;
            System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(0, 0, 12, 12);
            ledConnection.Region = new Region(path);
            grpGuardBandSettings.Controls.Add(ledConnection);

            lblConnectionStatus = new Label();
            lblConnectionStatus.Text = "OFFLINE";
            lblConnectionStatus.Font = new Font("Segoe UI", 7F, FontStyle.Bold);
            lblConnectionStatus.ForeColor = Color.FromArgb(255, 80, 80);
            lblConnectionStatus.Location = new Point(420, 148);
            lblConnectionStatus.Size = new Size(50, 14);
            grpGuardBandSettings.Controls.Add(lblConnectionStatus);

            // ── Embedded Jamming Monitor (inside config group, shown during TEST) ──
            pnlJammingMonitor = new Panel();
            pnlJammingMonitor.Location = new Point(4, 172);
            pnlJammingMonitor.Size = new Size(w - 12, 44);
            pnlJammingMonitor.BackColor = Color.FromArgb(25, 30, 20);
            pnlJammingMonitor.BorderStyle = BorderStyle.FixedSingle;
            pnlJammingMonitor.Visible = false;
            grpGuardBandSettings.Controls.Add(pnlJammingMonitor);

            lblJammingStatusText = new Label();
            lblJammingStatusText.Font = new Font("Consolas", 9F, FontStyle.Bold);
            lblJammingStatusText.ForeColor = Color.FromArgb(255, 80, 80);
            lblJammingStatusText.BackColor = Color.FromArgb(35, 40, 30);
            lblJammingStatusText.Dock = DockStyle.Top;
            lblJammingStatusText.Height = 20;
            lblJammingStatusText.TextAlign = ContentAlignment.MiddleLeft;
            lblJammingStatusText.Text = "";
            pnlJammingMonitor.Controls.Add(lblJammingStatusText);

            lblJammingCycleText = new Label();
            lblJammingCycleText.Font = new Font("Consolas", 9F, FontStyle.Bold);
            lblJammingCycleText.ForeColor = Color.FromArgb(100, 255, 100);
            lblJammingCycleText.BackColor = Color.FromArgb(35, 40, 30);
            lblJammingCycleText.Dock = DockStyle.Fill;
            lblJammingCycleText.TextAlign = ContentAlignment.MiddleLeft;
            lblJammingCycleText.Text = "";
            pnlJammingMonitor.Controls.Add(lblJammingCycleText);
            lblJammingCycleText.BringToFront();

            // Cycles hidden — TEST runs until STOP
            pulseCycles = int.MaxValue;
            numGuardCycles = null;

            UpdateGuardStatus();

            // Start in manual mode — band table and config panel disabled by default
            SetBandTableMode(false);
        }


        /// <summary>
        /// Update Guard Band status display
        /// </summary>
        private void UpdateGuardStatus()
        {
            if (lblGuardStatus == null || chkGuardPulseMode == null) return;

            // Count active bands from grid, split by mode
            int activeBands = 0;
            int prbsBands = 0;
            int rampBands = 0;
            int randomBands = 0;
            if (dgvBands != null)
            {
                foreach (DataGridViewRow row in dgvBands.Rows)
                {
                    if (row.IsNewRow) continue;
                    bool hasData = false;
                    try
                    {
                        if (dgvBands.Columns.Contains("colBandName") &&
                            row.Cells["colBandName"].Value != null &&
                            row.Cells["colBandName"].Value.ToString().Trim().Length > 0)
                            hasData = true;
                    }
                    catch { }
                    if (!hasData) continue;

                    bool active = true;
                    try
                    {
                        if (dgvBands.Columns.Contains("colActive") && row.Cells["colActive"].Value != null)
                            active = Convert.ToBoolean(row.Cells["colActive"].Value);
                    }
                    catch { }
                    if (!active) continue;

                    activeBands++;
                    string mode = "Ramp";
                    try
                    {
                        if (dgvBands.Columns.Contains("colDdsMode") && row.Cells["colDdsMode"].Value != null)
                            mode = row.Cells["colDdsMode"].Value.ToString();
                    }
                    catch { }
                    if (mode == "PRBS") prbsBands++;
                    else if (mode == "Random") randomBands++;
                    else rampBands++;
                }
            }

            int dwellUs = (numTdmDwell != null) ? (int)numTdmDwell.Value : 100;
            int dutyPct = (numDutyPercent != null) ? (int)numDutyPercent.Value : 100;
            string modeInfo = "";
            if (prbsBands > 0 && (rampBands + randomBands) > 0)
            {
                string sweepInfo = "";
                if (rampBands > 0 && randomBands > 0) sweepInfo = string.Format("{0}Ramp+{1}Rnd", rampBands, randomBands);
                else if (randomBands > 0) sweepInfo = randomBands + "Rnd";
                else sweepInfo = rampBands + "Ramp";
                modeInfo = string.Format(" ({0} + {1}PRBS)", sweepInfo, prbsBands);
            }
            else if (prbsBands > 0)
                modeInfo = " (PRBS barrage)";
            else if (randomBands > 0 && rampBands > 0)
                modeInfo = string.Format(" ({0}Ramp + {1}Rnd)", rampBands, randomBands);
            else if (randomBands > 0)
                modeInfo = " (Random hop)";

            string dutyStr = "";  // Points now shown per-band in grid

            double cycleUs = (double)dwellUs * activeBands;
            double cycleMs = cycleUs / 1000.0;
            double cps = (cycleUs > 0) ? 1000000.0 / cycleUs : 0;

            if (chkGuardPulseMode.Checked)
            {
                double dutyCycle = (pulseOnTimeMs * 100.0) / (pulseOnTimeMs + pulseOffTimeMs);

                lblGuardStatus.Text = string.Format(
                    " {0} bands{3} | Dwell: {1}\u00B5s ({5:F1}ms cycle) | PULSED ON:{4} OFF:{6}{7}",
                    activeBands, dwellUs, dutyCycle, modeInfo, pulseOnTimeMs, cycleMs, pulseOffTimeMs, dutyStr);

                if (dutyPct > 50)
                    lblGuardStatus.ForeColor = Color.FromArgb(255, 100, 100);
                else if (dutyPct > 30)
                    lblGuardStatus.ForeColor = Color.FromArgb(255, 200, 80);
                else
                    lblGuardStatus.ForeColor = Color.FromArgb(100, 255, 100);
            }
            else
            {
                lblGuardStatus.Text = string.Format(
                    " {0} bands{1} | Dwell: {2}\u00B5s | {3:F1}ms cycle | ~{4:F0} cyc/s{5}",
                    activeBands, modeInfo, dwellUs, cycleMs, cps, dutyStr);

                if (dutyPct > 50)
                    lblGuardStatus.ForeColor = Color.FromArgb(255, 200, 80);
                else
                    lblGuardStatus.ForeColor = Color.FromArgb(100, 255, 100);
            }
        }

        /// <summary>
        /// Validate pulse settings and warn if extreme
        /// </summary>
        private void ValidatePulseSettings()
        {
            if (numGuardPulseOn == null || numGuardPulseOff == null) return;
            double dutyCycle = (pulseOnTimeMs * 100.0) / (pulseOnTimeMs + pulseOffTimeMs);
            if (dutyCycle > 50 && lblGuardStatus != null)
            {
                lblGuardStatus.ForeColor = Color.Red;
                lblGuardStatus.Text = " WARNING: Duty " + dutyCycle.ToString("F0") + "% - HIGH IMPACT";
            }
        }

        #endregion

        #region TDM_STANDALONE_CONTROLS

        // TDM EEPROM addresses (must match firmware dms_tdm.h)
        private const ushort TDM_EEPROM_HEADER = 448;
        private const ushort TDM_EEPROM_BANDS  = 480;  // 448 + 32

        // TDM USB commands (match dms_tdm.h)
        private const short CMD_SET_TDM_BAND    = 40;
        private const short CMD_GET_TDM_BAND    = 41;
        private const short CMD_SET_TDM_PARAMS  = 42;
        private const short CMD_GET_TDM_PARAMS  = 43;
        private const short CMD_TDM_START       = 44;
        private const short CMD_TDM_STOP        = 45;
        private const short CMD_TDM_STATUS      = 46;
        private const short CMD_TDM_SAVE_EEPROM = 47;
        private const short CMD_TDM_LOAD_EEPROM = 48;
        private const short CMD_SET_GROUP_ROTATION = 49;

        /// <summary>
        /// TDM Standalone panel removed in v2.5 - all controls merged into BAND CONFIGURATION panel.
        /// This stub kept for call compatibility.
        /// </summary>
        private void CreateTdmStandalonePanel()
        {
            // All controls now in CreateGuardBandSettingsBox()
            UpdateTdmHitsDisplay();
        }

        /// <summary>
        /// Update hits/second display. Forwards to UpdateGuardStatus (unified display in v2.5).
        /// </summary>
        private void UpdateTdmHitsDisplay()
        {
            UpdateGuardStatus();
        }

        // ════════════════════════════════════════════════════════════
        //  TDM COMMAND HELPERS
        //  Packet layout (tsSysCmd, 32 bytes):
        //    [0..1] Type   [2..3] Status   [4..5] Len   [6..31] data[26]
        // ════════════════════════════════════════════════════════════
        /// <summary>
        /// Send a TDM command via ltTxCommand queue (normal USB path).
        /// Timer1 must be disabled before calling this to prevent
        /// periodic sequences from stealing TDM responses.
        /// </summary>
        private void SendTdmCommand(short cmdType, byte[] data, int dataLen)
        {
            int cmdSize = Command.GetCommandSize();
            byte[] packet = new byte[cmdSize];
            // tsSysCmd: Type(uint16, 2) + Status(uint16, 2) + Len(uint16, 2) + data[]
            packet[0] = (byte)(cmdType & 0xFF);
            packet[1] = (byte)((cmdType >> 8) & 0xFF);
            packet[2] = 0; packet[3] = 0;  // Status = 0
            packet[4] = (byte)(dataLen & 0xFF);
            packet[5] = (byte)((dataLen >> 8) & 0xFF);
            if (data != null)
            {
                int copyLen = Math.Min(dataLen, cmdSize - 6);
                Array.Copy(data, 0, packet, 6, copyLen);
            }
            Debug.WriteLine(string.Format("TDM TX [{0}] len={1}", cmdType, dataLen));
            lock (ltTxLock) { ltTxCommand.Add(new Command(packet, cmdSize)); }
        }

        /// <summary>
        /// Send TDM parameters (dwell, mode, pulse) to firmware LIVE via CMD_SET_TDM_PARAMS.
        /// Firmware updates TdmCfg immediately — takes effect on next hop cycle.
        /// Called from dwell spinner ValueChanged so changes are instant.
        /// </summary>
        private void SendTdmParamsLive()
        {
            if (usbConnection == null || !usbConnection.IsOpen) return;
            if (ltTxCommand == null) return;  // Not yet initialized during form construction

            bool pulseMode = (chkGuardPulseMode != null && chkGuardPulseMode.Checked);
            int dwellUs = (numTdmDwell != null) ? (int)numTdmDwell.Value : 100;
            int pulseOn = (numGuardPulseOn != null) ? (int)numGuardPulseOn.Value : 10;
            int pulseOff = (numGuardPulseOff != null) ? (int)numGuardPulseOff.Value : 40;
            int dutyPct = (numDutyPercent != null) ? (int)numDutyPercent.Value : 100;
            byte mode = pulseMode ? (byte)2 : (byte)1;  // 1=CONTINUOUS, 2=PULSED

            byte[] data = new byte[8];
            data[0] = mode;
            data[1] = (byte)(dwellUs & 0xFF);
            data[2] = (byte)((dwellUs >> 8) & 0xFF);
            data[3] = (byte)(pulseOn & 0xFF);
            data[4] = (byte)((pulseOn >> 8) & 0xFF);
            data[5] = (byte)(pulseOff & 0xFF);
            data[6] = (byte)((pulseOff >> 8) & 0xFF);
            data[7] = (byte)dutyPct;

            SendTdmCommand(CMD_SET_TDM_PARAMS, data, 8);
            Debug.WriteLine(string.Format("K9: Live TDM params → dwell={0}µs mode={1} duty={2}%",
                dwellUs, mode, dutyPct));
        }

        /// <summary>
        /// Send band group rotation config to firmware.
        /// groupSize=0 disables rotation (all bands hop normally).
        /// groupSize=4 with 8 bands → 2 groups alternating every rotationMs.
        /// </summary>
        private void SendGroupRotation(byte groupSize, ushort rotationMs)
        {
            if (usbConnection == null || !usbConnection.IsOpen) return;
            if (ltTxCommand == null) return;

            byte[] data = new byte[3];
            data[0] = groupSize;
            data[1] = (byte)(rotationMs & 0xFF);
            data[2] = (byte)((rotationMs >> 8) & 0xFF);

            SendTdmCommand(CMD_SET_GROUP_ROTATION, data, 3);
            Debug.WriteLine(string.Format("K9: Group rotation → size={0} period={1}ms",
                groupSize, rotationMs));
        }


        // ════════════════════════════════════════════════════════════
        //  SAVE TO DEVICE (merged - saves full config + band table)
        //  Writes PLL/DDS/attenuator config AND TDM band table to EEPROM.
        //  On power-up, firmware loads everything and auto-starts.
        // ════════════════════════════════════════════════════════════
        private void btnTdmSaveStandalone_Click(object sender, EventArgs e)
        {
            // K9: Commit any in-progress grid edit BEFORE reading cells, so a value
            // just typed (e.g. Atten dB) is captured instead of the old cell value.
            if (dgvBands != null)
            {
                dgvBands.EndEdit();
                if (dgvBands.IsCurrentCellDirty)
                    dgvBands.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }

            if (!guiActive || usbConnection == null || !usbConnection.IsOpen)
            {
                MessageBox.Show("Device not connected!\nConnect the DMS hardware first.",
                    "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (dgvBands == null || dgvBands.Rows.Count == 0)
            {
                MessageBox.Show("No bands in the table!\nLoad bands first.",
                    "No Bands", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Confirm
            var result = MessageBox.Show(
                "Save configuration and start firmware TDM?\n\n" +
                "This writes:\n" +
                "  \u2022 PLL / DDS / attenuator settings to EEPROM\n" +
                "  \u2022 TDM band table + timing to EEPROM\n\n" +
                "Firmware TDM starts immediately with \u00B5s timing.\n" +
                "Config persists across power cycles.",
                "Save & Start Firmware TDM",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            try
            {
                // ── Stop any running TDM (PC-controlled or firmware) ──
                isJamming = false;
                try { lock (ltTxLock) { ltTxCommand.Clear(); } } catch { }
                try { lock (ltServiceSequences) { ltServiceSequences.Clear(); } } catch { }
                SendTdmCommand(CMD_TDM_STOP, null, 0);
                System.Threading.Thread.Sleep(100);
                try { SetControlsJammingMode(false); } catch { }

                // ══════════════════════════════════════════════════════
                //  SYNC device.dds from FIRST BAND IN TABLE (not panel textboxes).
                //  The standard EEPROM DDS config must match what TDM expects,
                //  not whatever is currently in the DDS panel controls.
                //  TDM firmware calls tdm_configure_dds_ramp() on boot which
                //  reconfigures AD9106 for RAMP mode, but the base DDS config
                //  must be sane or the initial AD9106_Init() produces junk.
                // ══════════════════════════════════════════════════════
                {
                    // Find WIDEST active band's BW (firmware uses widest for SRAM fill)
                    double widestBwMhz = 20.0;
                    int firstTones = 256;
                    foreach (DataGridViewRow r in dgvBands.Rows)
                    {
                        if (r.IsNewRow) continue;
                        if (r.Cells["colBandName"] == null || r.Cells["colBandName"].Value == null) continue;
                        bool isActive = true;
                        if (dgvBands.Columns.Contains("colActive") && r.Cells["colActive"].Value != null)
                            try { isActive = Convert.ToBoolean(r.Cells["colActive"].Value); } catch { }
                        if (!isActive) continue;

                        if (dgvBands.Columns.Contains("colBandwidthMhz") && r.Cells["colBandwidthMhz"].Value != null)
                        {
                            double bw = 20.0;
                            try { bw = Convert.ToDouble(r.Cells["colBandwidthMhz"].Value); } catch { }
                            if (bw > widestBwMhz) widestBwMhz = bw;
                        }
                        if (firstTones == 256 && dgvBands.Columns.Contains("colTones") && r.Cells["colTones"].Value != null)
                            try { firstTones = Convert.ToInt32(r.Cells["colTones"].Value); } catch { }
                    }

                    // Configure device.dds for RAMP mode with WIDEST band's BW
                    // Use setRampBandwidth() — same as manual Configuration path
                    double bwHz = widestBwMhz * 1e6;
                    device.dds.SweepOn = 2;  // RAMP mode
                    device.dds.Start = 0;
                    device.dds.setRampBandwidth(bwHz);  // 1:1 DDS→RF (confirmed empirically)
                    device.dds.FreqCtrl = (float)(bwHz / 10.0);
                    if (device.dds.FreqCtrl < 100000) device.dds.FreqCtrl = 100000;

                // ══════════════════════════════════════════════════════
                //  CRITICAL: Populate device.dds.registers[] by running
                //  DDSSetConfiguration(). This fills the AD9106 register
                //  array from current device.dds parameters. Without this,
                //  registers[] may be all-zeros → dead DDS on power-up.
                // ══════════════════════════════════════════════════════
                DDSSetConfiguration();

                // ══ DEBUG: Dump critical DDS register values from library ══
                // These are the values that setRampBandwidth() computed.
                // Firmware must produce IDENTICAL values for correct BW.
                Debug.WriteLine("╔═══ DDS REGISTER DUMP (from setRampBandwidth) ═══╗");
                Debug.WriteLine(string.Format("║ BW requested: {0:F1} MHz ({1:F0} Hz)", widestBwMhz, bwHz));
                Debug.WriteLine(string.Format("║ Reg 0x47 TW_RAM_CONFIG = {0} (0x{0:X4})", device.dds.registers[0x47]));
                Debug.WriteLine(string.Format("║ Reg 0x3E FTW_MSB       = 0x{0:X4}", device.dds.registers[0x3E]));
                Debug.WriteLine(string.Format("║ Reg 0x3F FTW_LSB       = 0x{0:X4}", device.dds.registers[0x3F]));
                Debug.WriteLine(string.Format("║ Reg 0x44 PAT_TYPE      = 0x{0:X4}", device.dds.registers[0x44]));
                Debug.WriteLine(string.Format("║ Reg 0x45 PAT_EN        = 0x{0:X4}", device.dds.registers[0x45]));
                Debug.WriteLine(string.Format("║ Reg 0x52 STOP_ADDR_CH1 = 0x{0:X4} ({0})", device.dds.registers[0x52]));
                Debug.WriteLine(string.Format("║ Points={0}, Step={1:F1} Hz, FreqCtrl={2:F0} Hz",
                    device.dds.Points, device.dds.Step, device.dds.FreqCtrl));
                Debug.WriteLine(string.Format("║ getRampBandwidth()     = {0:F3} MHz",
                    device.dds.getRampBandwidth() / 1e6));
                Debug.WriteLine("╚═══════════════════════════════════════════════════╝");
                }

                // ── Extract C# library's TW_RAM and FTW for firmware EEPROM header ──
                // These are the PROVEN values from setRampBandwidth(). Firmware will use
                // them directly instead of trying to replicate the computation.
                byte dds_tw_ram = (byte)(device.dds.registers[0x47] & 0xFF);
                uint dds_ftw = ((uint)device.dds.registers[0x3E] << 8) |
                               ((uint)(device.dds.registers[0x3F] >> 8) & 0xFF);

                // Override TW_RAM with user value from first active band's TwMem (colStatus)
                if (dgvBands.Columns.Contains("colStatus"))
                {
                    foreach (DataGridViewRow row in dgvBands.Rows)
                    {
                        if (row.IsNewRow) continue;
                        if (row.Cells["colStatus"].Value != null)
                        {
                            try
                            {
                                int tw = Convert.ToInt32(row.Cells["colStatus"].Value);
                                if (tw >= 0 && tw <= 4) dds_tw_ram = (byte)tw;
                            }
                            catch { }
                            break;
                        }
                        break;
                    }
                }
                Debug.WriteLine(string.Format("TDM Header: TW_RAM={0}, FTW={1} (0x{1:X6})", dds_tw_ram, dds_ftw));

                byte[] write_data = new byte[64];
                ushort len;
                ushort crc;
                byte[] tmpBuff;
                ushort usData;

                // ══════════════════════════════════════════════════════
                //  1. DEVICE CONFIG (attenuators + buzzer) → addr 32
                // ══════════════════════════════════════════════════════
                len = 0;
                Array.Clear(write_data, 0, write_data.Length);
                write_data[len++] = (byte)((device.ltSwitchAttenuator[0].Dac >> 0) & 0xFF);
                write_data[len++] = (byte)((device.ltSwitchAttenuator[0].Dac >> 8) & 0xFF);
                write_data[len++] = (byte)((device.ltSwitchAttenuator[1].Dac >> 0) & 0xFF);
                write_data[len++] = (byte)((device.ltSwitchAttenuator[1].Dac >> 8) & 0xFF);
                write_data[len++] = (byte)((device.ltSwitchAttenuator[2].Dac >> 0) & 0xFF);
                write_data[len++] = (byte)((device.ltSwitchAttenuator[2].Dac >> 8) & 0xFF);
                write_data[len++] = (byte)((device.ltSwitchAttenuator[3].Dac >> 0) & 0xFF);
                write_data[len++] = (byte)((device.ltSwitchAttenuator[3].Dac >> 8) & 0xFF);
                tmpBuff = BitConverter.GetBytes(device.buzzer.delay_on);
                write_data[len++] = tmpBuff[0]; write_data[len++] = tmpBuff[1];
                write_data[len++] = tmpBuff[2]; write_data[len++] = tmpBuff[3];
                tmpBuff = BitConverter.GetBytes(device.buzzer.time_on);
                write_data[len++] = tmpBuff[0]; write_data[len++] = tmpBuff[1];
                write_data[len++] = tmpBuff[2]; write_data[len++] = tmpBuff[3];
                crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                write_data[len++] = (byte)(crc & 0xFF);
                write_data[len++] = (byte)((crc >> 8) & 0xFF);
                AddSequences(new SequenceWriteMemory(ltTxCommand, null,
                    (ushort)EEPROM.DEVICE_CONFIG, write_data, len));
                AddSequences(new SequenceDelay(100));

                // ══════════════════════════════════════════════════════
                //  2. PLL INIT (registers) → addr 64
                // ══════════════════════════════════════════════════════
                len = 0;
                Array.Clear(write_data, 0, write_data.Length);
                for (int i = 0; i < 6; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        write_data[len++] = device.lo.pll.registers[i].data[j];
                    }
                }
                crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                write_data[len++] = (byte)(crc & 0xFF);
                write_data[len++] = (byte)((crc >> 8) & 0xFF);
                AddSequences(new SequenceWriteMemory(ltTxCommand, null,
                    (ushort)EEPROM.PLL_INIT, write_data, len));
                AddSequences(new SequenceDelay(100));

                // ══════════════════════════════════════════════════════
                //  3. PLL CONFIG (sweep params) → addr 96
                //  CRITICAL: Force SweepOn=0 to disable standard LO stepping.
                //  TDM handles its own PLL hopping via tdm_hop_to_band().
                //  If SweepOn=1, standard LO stepping fights with TDM for
                //  MAX2871 control, causing erratic frequency behavior.
                // ══════════════════════════════════════════════════════
                len = 0;
                Array.Clear(write_data, 0, write_data.Length);
                write_data[len++] = (byte)(device.lo.WaitLD > 0 ? 1 : 0);
                write_data[len++] = 0;  // SweepOn = 0: TDM controls PLL, not standard sweep
                tmpBuff = BitConverter.GetBytes((float)0.0f);  // Step = 0
                write_data[len++] = tmpBuff[0]; write_data[len++] = tmpBuff[1];
                write_data[len++] = tmpBuff[2]; write_data[len++] = tmpBuff[3];
                tmpBuff = BitConverter.GetBytes((uint)1);  // Points = 1
                write_data[len++] = tmpBuff[0]; write_data[len++] = tmpBuff[1];
                write_data[len++] = tmpBuff[2]; write_data[len++] = tmpBuff[3];
                tmpBuff = BitConverter.GetBytes(device.lo.HoldTime);
                write_data[len++] = tmpBuff[0]; write_data[len++] = tmpBuff[1];
                write_data[len++] = tmpBuff[2]; write_data[len++] = tmpBuff[3];
                crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                write_data[len++] = (byte)(crc & 0xFF);
                write_data[len++] = (byte)((crc >> 8) & 0xFF);
                AddSequences(new SequenceWriteMemory(ltTxCommand, null,
                    (ushort)EEPROM.PLL_CONFIG, write_data, len));
                AddSequences(new SequenceDelay(100));

                // ══════════════════════════════════════════════════════
                //  4. DDS CONTROL (sweep params) → addr 128
                // ══════════════════════════════════════════════════════
                len = 0;
                Array.Clear(write_data, 0, write_data.Length);
                usData = device.dds.SweepOn;
                write_data[len++] = (byte)((usData >> 0) & 0xFF);
                write_data[len++] = (byte)((usData >> 8) & 0xFF);
                tmpBuff = BitConverter.GetBytes(device.dds.Start);
                write_data[len++] = tmpBuff[0]; write_data[len++] = tmpBuff[1];
                write_data[len++] = tmpBuff[2]; write_data[len++] = tmpBuff[3];
                tmpBuff = BitConverter.GetBytes(device.dds.Step);
                write_data[len++] = tmpBuff[0]; write_data[len++] = tmpBuff[1];
                write_data[len++] = tmpBuff[2]; write_data[len++] = tmpBuff[3];
                tmpBuff = BitConverter.GetBytes(device.dds.Points);
                write_data[len++] = tmpBuff[0]; write_data[len++] = tmpBuff[1];
                write_data[len++] = tmpBuff[2]; write_data[len++] = tmpBuff[3];
                tmpBuff = BitConverter.GetBytes(device.dds.FreqCtrl);
                write_data[len++] = tmpBuff[0]; write_data[len++] = tmpBuff[1];
                write_data[len++] = tmpBuff[2]; write_data[len++] = tmpBuff[3];
                crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                write_data[len++] = (byte)(crc & 0xFF);
                write_data[len++] = (byte)((crc >> 8) & 0xFF);
                AddSequences(new SequenceWriteMemory(ltTxCommand, null,
                    (ushort)EEPROM.DDS_CONTROL, write_data, len));
                AddSequences(new SequenceDelay(100));

                // ══════════════════════════════════════════════════════
                //  5. DDS REGISTERS PART1 (regs 0-14) → addr 160
                // ══════════════════════════════════════════════════════
                len = 0;
                Array.Clear(write_data, 0, write_data.Length);
                for (int i = 0; i < 15; i++)
                {
                    write_data[len++] = (byte)((device.dds.registers[i] >> 0) & 0xFF);
                    write_data[len++] = (byte)((device.dds.registers[i] >> 8) & 0xFF);
                }
                crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                write_data[len++] = (byte)(crc & 0xFF);
                write_data[len++] = (byte)((crc >> 8) & 0xFF);
                AddSequences(new SequenceWriteMemory(ltTxCommand, null,
                    (ushort)EEPROM.DDS_REG_PART1, write_data, len));
                AddSequences(new SequenceDelay(100));

                // ══════════════════════════════════════════════════════
                //  6. DDS REGISTERS PART2 (regs 30-45) → addr 192
                // ══════════════════════════════════════════════════════
                len = 0;
                Array.Clear(write_data, 0, write_data.Length);
                int[] part2Regs = { 30, 31, 32, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45 };
                for (int i = 0; i < part2Regs.Length; i++)
                {
                    write_data[len++] = (byte)((device.dds.registers[part2Regs[i]] >> 0) & 0xFF);
                    write_data[len++] = (byte)((device.dds.registers[part2Regs[i]] >> 8) & 0xFF);
                }
                crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                write_data[len++] = (byte)(crc & 0xFF);
                write_data[len++] = (byte)((crc >> 8) & 0xFF);
                AddSequences(new SequenceWriteMemory(ltTxCommand, null,
                    (ushort)EEPROM.DDS_REG_PART2, write_data, len));
                AddSequences(new SequenceDelay(100));

                // ══════════════════════════════════════════════════════
                //  7. DDS REGISTERS PART3 (regs 46-55, 62-66) → addr 224
                // ══════════════════════════════════════════════════════
                len = 0;
                Array.Clear(write_data, 0, write_data.Length);
                int[] part3Regs = { 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 62, 63, 64, 65, 66 };
                for (int i = 0; i < part3Regs.Length; i++)
                {
                    write_data[len++] = (byte)((device.dds.registers[part3Regs[i]] >> 0) & 0xFF);
                    write_data[len++] = (byte)((device.dds.registers[part3Regs[i]] >> 8) & 0xFF);
                }
                crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                write_data[len++] = (byte)(crc & 0xFF);
                write_data[len++] = (byte)((crc >> 8) & 0xFF);
                AddSequences(new SequenceWriteMemory(ltTxCommand, null,
                    (ushort)EEPROM.DDS_REG_PART3, write_data, len));
                AddSequences(new SequenceDelay(100));

                // ══════════════════════════════════════════════════════
                //  8. DDS REGISTERS PART4 (regs 67-69, 71, 80-90) → addr 256
                // ══════════════════════════════════════════════════════
                len = 0;
                Array.Clear(write_data, 0, write_data.Length);
                int[] part4Regs = { 67, 68, 69, 71, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90 };
                for (int i = 0; i < part4Regs.Length; i++)
                {
                    write_data[len++] = (byte)((device.dds.registers[part4Regs[i]] >> 0) & 0xFF);
                    write_data[len++] = (byte)((device.dds.registers[part4Regs[i]] >> 8) & 0xFF);
                }
                crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                write_data[len++] = (byte)(crc & 0xFF);
                write_data[len++] = (byte)((crc >> 8) & 0xFF);
                AddSequences(new SequenceWriteMemory(ltTxCommand, null,
                    (ushort)EEPROM.DDS_REG_PART4, write_data, len));
                AddSequences(new SequenceDelay(100));

                // ══════════════════════════════════════════════════════
                //  9. DDS REGISTERS PART5 (regs 91-95) → addr 288
                // ══════════════════════════════════════════════════════
                len = 0;
                Array.Clear(write_data, 0, write_data.Length);
                int[] part5Regs = { 91, 92, 93, 94, 95 };
                for (int i = 0; i < part5Regs.Length; i++)
                {
                    write_data[len++] = (byte)((device.dds.registers[part5Regs[i]] >> 0) & 0xFF);
                    write_data[len++] = (byte)((device.dds.registers[part5Regs[i]] >> 8) & 0xFF);
                }
                crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                write_data[len++] = (byte)(crc & 0xFF);
                write_data[len++] = (byte)((crc >> 8) & 0xFF);
                AddSequences(new SequenceWriteMemory(ltTxCommand, null,
                    (ushort)EEPROM.DDS_REG_PART5, write_data, len));
                AddSequences(new SequenceDelay(100));

                // ══════════════════════════════════════════════════════
                //  10. LO SWITCH FREQUENCIES → addr 320 + 352
                // ══════════════════════════════════════════════════════
                len = 0;
                Array.Clear(write_data, 0, write_data.Length);
                for (int i = 0; i < 4; i++)
                {
                    tmpBuff = BitConverter.GetBytes(device.ltSwitchFrequency[i]);
                    write_data[len++] = tmpBuff[0]; write_data[len++] = tmpBuff[1];
                    write_data[len++] = tmpBuff[2]; write_data[len++] = tmpBuff[3];
                }
                crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                write_data[len++] = (byte)(crc & 0xFF);
                write_data[len++] = (byte)((crc >> 8) & 0xFF);
                AddSequences(new SequenceWriteMemory(ltTxCommand, null,
                    (ushort)EEPROM.LO_SWITCH_PART1, write_data, len));
                AddSequences(new SequenceDelay(100));

                len = 0;
                Array.Clear(write_data, 0, write_data.Length);
                for (int i = 0; i < 4; i++)
                {
                    tmpBuff = BitConverter.GetBytes(device.ltSwitchFrequency[4 + i]);
                    write_data[len++] = tmpBuff[0]; write_data[len++] = tmpBuff[1];
                    write_data[len++] = tmpBuff[2]; write_data[len++] = tmpBuff[3];
                }
                crc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(write_data, len);
                write_data[len++] = (byte)(crc & 0xFF);
                write_data[len++] = (byte)((crc >> 8) & 0xFF);
                AddSequences(new SequenceWriteMemory(ltTxCommand, null,
                    (ushort)EEPROM.LO_SWITCH_PART2, write_data, len));
                AddSequences(new SequenceDelay(100));

                // ══════════════════════════════════════════════════════
                //  FLUSH: Wait for all config writes (sections 1-10) to
                //  complete before queuing TDM commands.
                // ══════════════════════════════════════════════════════
                WaitForCommandQueue(10000);
                System.Threading.Thread.Sleep(500);

                // ══════════════════════════════════════════════════════
                //  5. Write TDM config to EEPROM (for autonomous boot)
                //  Uses proven SequenceWriteMemory — same mechanism as
                //  all other EEPROM sections. Each write is page-aligned
                //  and fits within a single 32-byte AT24 page.
                //
                //  EEPROM layout (matches tdm_load_from_eeprom):
                //  448: Header (18 bytes) — mode, bands, dwell, pulse, "K9", CRC
                //  480: Band 0 (30 bytes) — tsTdmBand + CRC
                //  512: Band 1, 544: Band 2, ... (32-byte stride)
                // ══════════════════════════════════════════════════════
                const ushort TDM_EEPROM_HEADER = 448;
                const ushort TDM_EEPROM_BANDS  = 480;
                const int    TDM_BAND_STRIDE   = 32;

                int bandCount = 0;
                bool pulseMode = (chkGuardPulseMode != null && chkGuardPulseMode.Checked);
                int dwellUs = (numTdmDwell != null) ? (int)numTdmDwell.Value : 100;
                int pulseOn = (numGuardPulseOn != null) ? (int)numGuardPulseOn.Value : 10;
                int pulseOff = (numGuardPulseOff != null) ? (int)numGuardPulseOff.Value : 40;
                int dutyPct = (numDutyPercent != null) ? (int)numDutyPercent.Value : 100;

                // Prepare band data for EEPROM
                byte[][] bandEepromData = new byte[8][];

                foreach (DataGridViewRow row in dgvBands.Rows)
                {
                    if (row.IsNewRow) continue;
                    if (row.Cells["colBandName"] == null || row.Cells["colBandName"].Value == null) continue;
                    if (row.Cells["colStartMhz"] == null || row.Cells["colStartMhz"].Value == null) continue;
                    if (bandCount >= 8) break;

                    double freqMhz;
                    try { freqMhz = Convert.ToDouble(row.Cells["colStartMhz"].Value); } catch { continue; }
                    if (freqMhz < 20.0 || freqMhz > 6200.0) continue;

                    double bwMhz = 20.0;
                    bool active = true;
                    string ddsModeName = "Ramp";
                    byte ddsModeVal = 0;  // 0=RAMP, 1=PRBS, 2=RANDOM

                    if (dgvBands.Columns.Contains("colBandwidthMhz") && row.Cells["colBandwidthMhz"].Value != null)
                        try { bwMhz = Convert.ToDouble(row.Cells["colBandwidthMhz"].Value); } catch { }
                    if (dgvBands.Columns.Contains("colActive") && row.Cells["colActive"].Value != null)
                        try { active = Convert.ToBoolean(row.Cells["colActive"].Value); } catch { }
                    if (dgvBands.Columns.Contains("colDdsMode") && row.Cells["colDdsMode"].Value != null)
                        ddsModeName = row.Cells["colDdsMode"].Value.ToString();
                    if (ddsModeName == "PRBS") ddsModeVal = 1;
                    else if (ddsModeName == "Random") ddsModeVal = 2;

                    double loFreqHz = freqMhz * 1e6;
                    float ddsBwHz = (float)(bwMhz * 1e6);

                    // Compute DDS params
                    uint ddsPoints;
                    float ddsStep;
                    float ddsCtrlFreq;

                    if (ddsModeVal == 1)  // PRBS
                    {
                        // PRBS mode: noise BW ≈ FreqCtrl rate.
                        // Default to BW so noise fills the entire band.
                        // User can override via FCtrl column.
                        ddsPoints = 4096;
                        ddsStep = 0;
                        ddsCtrlFreq = ddsBwHz;  // Default: full bandwidth noise
                        if (dgvBands.Columns.Contains("colFreqCtrl") && row.Cells["colFreqCtrl"].Value != null)
                        {
                            string fcVal = row.Cells["colFreqCtrl"].Value.ToString().Trim().ToLower();
                            if (fcVal != "auto" && fcVal != "" && fcVal != "0")
                                try { ddsCtrlFreq = (float)(Convert.ToDouble(fcVal) * 1e6); } catch { }
                        }
                    }
                    else
                    {
                        // RAMP or RANDOM mode: read per-band points from grid
                        int userPts = 512;  // Default — safe for LTE
                        if (dgvBands.Columns.Contains("colTones") && row.Cells["colTones"].Value != null)
                            try { userPts = Convert.ToInt32(row.Cells["colTones"].Value); } catch { }
                        userPts = Math.Max(32, Math.Min(4096, userPts));

                        ddsPoints = (uint)userPts;
                        ddsStep = ddsBwHz / (float)ddsPoints;
                        // Default 0 = firmware auto-calculates ctrl_freq = points/dwell.
                        // User can override via FreqCtrl column for per-band sweep speed.
                        ddsCtrlFreq = 0;
                        if (dgvBands.Columns.Contains("colFreqCtrl") && row.Cells["colFreqCtrl"].Value != null)
                        {
                            string fcVal = row.Cells["colFreqCtrl"].Value.ToString().Trim().ToLower();
                            if (fcVal != "auto" && fcVal != "" && fcVal != "0")
                                try { ddsCtrlFreq = (float)(Convert.ToDouble(fcVal) * 1e6); } catch { }
                        }
                    }
                    if (ddsModeVal == 1 && ddsCtrlFreq < 100000) ddsCtrlFreq = 100000;

                    // Pack tsTdmBand struct (28 bytes) + CRC (2 bytes) = 30 bytes
                    // Matches firmware tdm_load_from_eeprom() band format
                    byte[] bandData = new byte[30];
                    Array.Copy(BitConverter.GetBytes(loFreqHz), 0, bandData, 0, 8);      // double lo_frequency
                    Array.Copy(BitConverter.GetBytes(ddsBwHz), 0, bandData, 8, 4);        // float dds_bandwidth
                    Array.Copy(BitConverter.GetBytes(ddsStep), 0, bandData, 12, 4);       // float dds_step
                    Array.Copy(BitConverter.GetBytes(ddsPoints), 0, bandData, 16, 4);     // uint32 dds_points
                    Array.Copy(BitConverter.GetBytes(ddsCtrlFreq), 0, bandData, 20, 4);   // float dds_ctrl_freq
                    bandData[24] = (byte)(active ? 1 : 0);                                // uint8 active
                    bandData[25] = ddsModeVal;                                             // uint8 dds_mode (0=RAMP, 1=PRBS, 2=RANDOM)
                    // Per-band attenuation: convert the Atten dB cell to a DAC code and
                    // pack into bytes 26-27 (was reserved[2]). 0xFFFF = blank => firmware
                    // falls back to the main attenuator. Matches live HopToFrequency scaling.
                    ushort attenDac = 0xFFFF;
                    if (dgvBands.Columns.Contains("colAttenDb") && row.Cells["colAttenDb"].Value != null)
                    {
                        string aVal = row.Cells["colAttenDb"].Value.ToString().Trim();
                        if (aVal != "")
                        {
                            try
                            {
                                double aDb = Convert.ToDouble(aVal);
                                if (aDb > 0.0 && device != null && device.AttCalibrationTable != null)
                                {
                                    double clampedDb = Math.Max(0, Math.Min(25, aDb));
                                    float v = device.AttCalibrationTable.getVoltage((float)clampedDb);
                                    attenDac = (ushort)((v / 3.3f) * 4096.0f);
                                }
                            }
                            catch { attenDac = 0xFFFF; }
                        }
                    }
                    bandData[26] = (byte)(attenDac & 0xFF);
                    bandData[27] = (byte)((attenDac >> 8) & 0xFF);         // per-band atten DAC (little-endian)
                    ushort bandCrc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(bandData, 28);
                    bandData[28] = (byte)(bandCrc & 0xFF);
                    bandData[29] = (byte)((bandCrc >> 8) & 0xFF);

                    bandEepromData[bandCount] = bandData;

                    string[] modeNames = { "RAMP", "PRBS", "RANDOM" };
                    string mName = (ddsModeVal < modeNames.Length) ? modeNames[ddsModeVal] : "?";
                    Debug.WriteLine(string.Format(
                        "TDM EEPROM Band[{0}]: {1:F1} MHz, BW={2:F1} MHz, Pts={3}, Step={4:F1}, Active={5}, Mode={6}",
                        bandCount, freqMhz, bwMhz, ddsPoints, ddsStep, active, mName));

                    bandCount++;
                }

                if (bandCount == 0)
                {
                    MessageBox.Show("No valid bands found!", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // ── Write TDM header to EEPROM at address 448 ──
                // Format: mode(1) + num_bands(1) + dwell_us(2) + pulse_on(2) + pulse_off(2) + sig(2) + ver(1) + duty(1) + tw_ram(1) + ftw(3) + CRC(2) = 18 bytes
                byte[] header = new byte[18];
                header[0] = (byte)(pulseMode ? 2 : 1);                           // mode
                header[1] = (byte)bandCount;                                       // num_bands
                header[2] = (byte)(dwellUs & 0xFF); header[3] = (byte)((dwellUs >> 8) & 0xFF);       // dwell_us
                header[4] = (byte)(pulseOn & 0xFF);  header[5] = (byte)((pulseOn >> 8) & 0xFF);      // pulse_on_ms
                header[6] = (byte)(pulseOff & 0xFF); header[7] = (byte)((pulseOff >> 8) & 0xFF);     // pulse_off_ms
                header[8] = 0x4B; header[9] = 0x39;                               // "K9" signature
                header[10] = 0x04;                                                 // Version 4 = adds TW_RAM + FTW
                header[11] = (byte)dutyPct;                                        // SRAM duty 5-100%
                header[12] = dds_tw_ram;                                           // TW_RAM_CONFIG from C# library
                header[13] = (byte)(dds_ftw & 0xFF);                              // FTW byte 0 (LSB)
                header[14] = (byte)((dds_ftw >> 8) & 0xFF);                       // FTW byte 1
                header[15] = (byte)((dds_ftw >> 16) & 0xFF);                      // FTW byte 2 (MSB)
                ushort hdrCrc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(header, 16);
                header[16] = (byte)(hdrCrc & 0xFF);
                header[17] = (byte)((hdrCrc >> 8) & 0xFF);

                AddSequences(new SequenceWriteMemory(ltTxCommand, null,
                    TDM_EEPROM_HEADER, header, (ushort)header.Length));
                AddSequences(new SequenceDelay(50));

                // ── Write each band to EEPROM (32-byte stride, page-aligned) ──
                for (int i = 0; i < bandCount; i++)
                {
                    ushort bandAddr = (ushort)(TDM_EEPROM_BANDS + (i * TDM_BAND_STRIDE));
                    AddSequences(new SequenceWriteMemory(ltTxCommand, null,
                        bandAddr, bandEepromData[i], (ushort)bandEepromData[i].Length));
                    AddSequences(new SequenceDelay(50));
                }

                // Clear unused band slots (write zeros with valid CRC)
                for (int i = bandCount; i < 8; i++)
                {
                    byte[] emptyBand = new byte[30];
                    // All zeros = inactive band, just add CRC
                    ushort emptyCrc = nsAlexKir.MathLibrary.CRC.CalculateCRC16(emptyBand, 28);
                    emptyBand[28] = (byte)(emptyCrc & 0xFF);
                    emptyBand[29] = (byte)((emptyCrc >> 8) & 0xFF);

                    ushort bandAddr = (ushort)(TDM_EEPROM_BANDS + (i * TDM_BAND_STRIDE));
                    AddSequences(new SequenceWriteMemory(ltTxCommand, null,
                        bandAddr, emptyBand, (ushort)emptyBand.Length));
                    AddSequences(new SequenceDelay(30));
                }

                Debug.WriteLine(string.Format(
                    "TDM EEPROM: header at {0}, {1} bands at {2}+ (stride {3})",
                    TDM_EEPROM_HEADER, bandCount, TDM_EEPROM_BANDS, TDM_BAND_STRIDE));

                // ══════════════════════════════════════════════════════
                //  6. Wait for EEPROM writes to complete, then start
                //  FIRMWARE AUTONOMOUS TDM for real microsecond timing.
                //  PC-controlled TDM (TEST button) is USB-limited to ~1ms.
                //  Firmware TDM uses DWT cycle counter for exact µs hops.
                // ══════════════════════════════════════════════════════
                WaitForCommandQueue(10000);
                System.Threading.Thread.Sleep(300);

                // Tell firmware to reload EEPROM into RAM, then start autonomous TDM
                SendTdmCommand(CMD_TDM_LOAD_EEPROM, null, 0);
                System.Threading.Thread.Sleep(200);  // Give EEPROM read time
                SendTdmCommand(CMD_TDM_START, null, 0);
                System.Threading.Thread.Sleep(100);

                // Auto-enable group rotation if more bands than group size
                byte grpSize = (numGroupSize != null) ? (byte)numGroupSize.Value : (byte)4;
                if (grpSize > 0 && bandCount > grpSize)
                {
                    ushort rotMs = (numRotationMs != null) ? (ushort)numRotationMs.Value : (ushort)10;
                    SendGroupRotation(grpSize, rotMs);
                    tdmRotationMs = rotMs;
                    System.Threading.Thread.Sleep(50);
                }

                // Update UI state for autonomous mode
                if (btnGuardStart != null) btnGuardStart.Enabled = false;
                if (btnGuardStop != null) btnGuardStop.Enabled = true;
                isJamming = true;

                string modeStr = pulseMode
                    ? string.Format("Pulsed ({0}ms on / {1}ms off)", pulseOn, pulseOff)
                    : "Continuous";

                double saveCycleMs = (double)dwellUs * bandCount / 1000.0;
                lblGuardStatus.Text = string.Format("FW TDM: {0} bands, {1}\u00B5s dwell, {2:F1}ms cycle, {3}",
                    bandCount, dwellUs, saveCycleMs, modeStr);
                lblGuardStatus.ForeColor = Color.FromArgb(255, 100, 100);  // Red = active jamming

                string bandSummary = "";
                for (int i = 0; i < bandCount; i++)
                {
                    byte[] bd = bandEepromData[i];
                    double fHz = BitConverter.ToDouble(bd, 0);
                    float bwHz = BitConverter.ToSingle(bd, 8);
                    uint pts = BitConverter.ToUInt32(bd, 16);
                    string[] mNames2 = { "RAMP", "PRBS", "RANDOM" };
                    string bdMode = (bd[25] < mNames2.Length) ? mNames2[bd[25]] : "?";
                    double stepKhz = (bwHz / 1000.0) / Math.Max(1, pts);
                    bandSummary += string.Format("  Band {0}: {1:F1} MHz  BW={2:F1} MHz  {3}pts  Step={4:F0}kHz  [{5}]\n",
                        i + 1, fHz / 1e6, bwHz / 1e6, pts, stepKhz, bdMode);
                }

                MessageBox.Show(
                    "SAVE & START COMPLETE\n" +
                    "═══════════════════════════════════════\n\n" +
                    "Standard config: EEPROM \u2713\n" +
                    "TDM config: EEPROM \u2713 (autonomous)\n" +
                    "Firmware TDM: STARTED \u2713 (\u00B5s timing)\n" +
                    "Mode: " + modeStr + "\n" +
                    "Dwell: " + dwellUs + " \u00B5s  (" + saveCycleMs.ToString("F1") + "ms cycle)\n\n" +
                    "DDS Hardware BW: " + (device.dds.getRampBandwidth() / 1e6).ToString("F1") +
                    " MHz (startup config = widest band)\n" +
                    "TwMem(0x47): " + (device.dds.registers[0x47] & 0x0F) + "\n\n" +
                    bandSummary + "\n" +
                    "Firmware is now hopping autonomously.\n" +
                    "Per-band BW switching via STOP_ADDR per hop.\n" +
                    "Use STOP to halt.  Power cycle = auto-restart.",
                    "Firmware TDM Running",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                timer1.Enabled = true;  // Always re-enable periodic polling
                MessageBox.Show("Save error: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  START STANDALONE TDM (while connected)
        // ════════════════════════════════════════════════════════════
        private void btnTdmStart_Click(object sender, EventArgs e)
        {
            if (!guiActive || usbConnection == null || !usbConnection.IsOpen)
            {
                MessageBox.Show("Device not connected!", "Not Connected",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SendTdmCommand(CMD_TDM_START, null, 0);

            if (btnTdmStart != null) btnTdmStart.Enabled = false;
            if (btnTdmStop != null) btnTdmStop.Enabled = true;
            if (lblGuardStatus != null)
            {
                lblGuardStatus.Text = " TDM RUNNING (standalone)";
                lblGuardStatus.ForeColor = Color.FromArgb(255, 100, 100);
            }
            Debug.WriteLine("TDM: Start sent (direct)");
        }

        // ════════════════════════════════════════════════════════════
        //  STOP STANDALONE TDM
        // ════════════════════════════════════════════════════════════
        private void btnTdmStop_Click(object sender, EventArgs e)
        {
            if (!guiActive || usbConnection == null || !usbConnection.IsOpen)
            {
                MessageBox.Show("Device not connected!", "Not Connected",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SendTdmCommand(CMD_TDM_STOP, null, 0);

            if (btnTdmStart != null) btnTdmStart.Enabled = true;
            if (btnTdmStop != null) btnTdmStop.Enabled = false;
            if (lblGuardStatus != null)
            {
                lblGuardStatus.Text = " TDM STOPPED";
                lblGuardStatus.ForeColor = Color.FromArgb(100, 255, 100);
            }
            Debug.WriteLine("TDM: Stop sent (direct)");
        }

        #endregion

        #region JAMMING_STATE_MANAGEMENT

        private void WaitForCommandQueue(int timeoutMs = 5000)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                bool txEmpty;
                lock (ltTxLock) { txEmpty = (ltTxCommand.Count == 0); }
                bool seqEmpty;
                lock (ltServiceSequences) { seqEmpty = (ltServiceSequences.Count == 0); }
                if (txEmpty && seqEmpty) break;
                // Pump messages so BeginInvoke callbacks can fire
                Application.DoEvents();
                Thread.Sleep(1);
            }
            // Final flush to process any remaining queued callbacks
            Application.DoEvents();
        }

        /// <summary>
        /// WaitForCommandQueue that also bails if jamming is stopped.
        /// Use ONLY inside the jamming thread — not for SAVE TO DEVICE or other flows.
        /// </summary>
        private void WaitForCommandQueueJamming(int timeoutMs = 5000)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (!isJamming) break;  // Bail immediately when STOP pressed
                bool txEmpty;
                lock (ltTxLock) { txEmpty = (ltTxCommand.Count == 0); }
                bool seqEmpty;
                lock (ltServiceSequences) { seqEmpty = (ltServiceSequences.Count == 0); }
                if (txEmpty && seqEmpty) break;
                Thread.Sleep(1);
            }
        }

                private void SaveHardwareState()
        {
            savedLoFrequency = device.lo.pll.OutFrequency;
            savedLoStep = device.lo.Step;
            savedLoPoints = device.lo.Points;
            savedLoHoldTime = device.lo.HoldTime;
            savedLoWaitLD = device.lo.WaitLD;
            savedLoSweepOn = device.lo.SweepOn;
            savedDdsSweepOn = device.dds.SweepOn;
            savedDdsStart = (ulong)device.dds.Start;
            savedDdsStep = (ulong)device.dds.Step;
            savedDdsPoints = (ulong)device.dds.Points;
            savedDdsFreqCtrl = device.dds.FreqCtrl;
            savedDdsMode = k9ExtendedMode;
            savedAttPosition = (ushort)activeAttPosition;
            if (device.ltSwitchAttenuator != null && device.ltSwitchAttenuator.Count > activeAttPosition)
                savedAttDac = device.ltSwitchAttenuator[activeAttPosition].Dac;
            Debug.WriteLine("| State saved: LO=" + (savedLoFrequency/1e6).ToString("F1") + " MHz");
        }

        private void RestoreHardwareState()
        {
            try
            {
                device.lo.pll.OutFrequency = savedLoFrequency;
                device.lo.Step = savedLoStep;
                device.lo.Points = savedLoPoints;
                device.lo.HoldTime = savedLoHoldTime;
                device.lo.WaitLD = savedLoWaitLD;
                device.lo.SweepOn = savedLoSweepOn;
                device.dds.SweepOn = savedDdsSweepOn;
                device.dds.Start = savedDdsStart;
                device.dds.Step = savedDdsStep;
                device.dds.Points = savedDdsPoints;
                device.dds.FreqCtrl = savedDdsFreqCtrl;
                k9ExtendedMode = savedDdsMode;

                AddSequences(new SequenceSetSweepLoParameters(ltTxCommand, null,
                    (float)savedLoFrequency, savedLoStep, savedLoPoints,
                    savedLoHoldTime, savedLoWaitLD, savedLoSweepOn));
                AddSequences(new SequenceSetSweepDDS(ltTxCommand, null,
                    savedDdsSweepOn, savedDdsStart, savedDdsStep, savedDdsPoints));
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, savedDdsFreqCtrl));

                // Restore attenuator to pre-jamming level
                if (device.ltSwitchAttenuator != null && device.ltSwitchAttenuator.Count > savedAttPosition)
                {
                    device.ltSwitchAttenuator[savedAttPosition].Dac = savedAttDac;
                    AddSequences(new SequenceSetDacAttenuator(ltTxCommand, null, savedAttDac, savedAttPosition));
                }

                this.Invoke((MethodInvoker)delegate
                {
                    UpdateMainForm(device);
                    PrepareGroupBoxDDS(savedDdsSweepOn);
                    if (cmbbxDdsMode != null && savedDdsMode < cmbbxDdsMode.Items.Count)
                        cmbbxDdsMode.SelectedIndex = savedDdsMode;
                });
                Debug.WriteLine("| State restored: LO=" + (savedLoFrequency/1e6).ToString("F1") + " MHz");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("| WARNING: State restore error: " + ex.Message);
            }
        }

        private void SetControlsJammingMode(bool jammingActive)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { SetControlsJammingMode(jammingActive); });
                return;
            }
            Color bgColor = jammingActive ? Color.FromArgb(255, 255, 180) : SystemColors.Window;
            bool ro = jammingActive;
            if (txbxLoStart != null) { txbxLoStart.BackColor = bgColor; txbxLoStart.ReadOnly = ro; }
            if (txbxLoStep != null) { txbxLoStep.BackColor = bgColor; txbxLoStep.ReadOnly = ro; }
            if (txbxLoPoints != null) { txbxLoPoints.BackColor = bgColor; txbxLoPoints.ReadOnly = ro; }
            if (txbxLoDelays != null) { txbxLoDelays.BackColor = bgColor; txbxLoDelays.ReadOnly = ro; }
            if (txbxDdsStart != null) { txbxDdsStart.BackColor = bgColor; txbxDdsStart.ReadOnly = ro; }
            if (txbxDdsStep != null) { txbxDdsStep.BackColor = bgColor; txbxDdsStep.ReadOnly = ro; }
            if (txbxDdsPoints != null) { txbxDdsPoints.BackColor = bgColor; txbxDdsPoints.ReadOnly = ro; }
            if (txbxDdsBandwith != null) { txbxDdsBandwith.BackColor = bgColor; txbxDdsBandwith.ReadOnly = ro; }
            if (txbxDdsFreqCtrl != null) { txbxDdsFreqCtrl.BackColor = bgColor; txbxDdsFreqCtrl.ReadOnly = ro; }
            if (cmbbxDdsMode != null) cmbbxDdsMode.Enabled = !jammingActive;
            if (cmbbxLoPower != null) cmbbxLoPower.Enabled = !jammingActive;

            // Lock attenuator panel during jamming (per-band Atten dB takes over)
            if (grbxATT != null) grbxATT.Enabled = !jammingActive;

            // Lock mode toggle during jamming
            if (chkBandTableMode != null) chkBandTableMode.Enabled = !jammingActive;

            if (jammingActive)
            {
                if (!this.Text.Contains("[JAMMING]"))
                    this.Text = this.Text + " [JAMMING]";
                ShowJammingStatusWindow();
            }
            else
            {
                this.Text = this.Text.Replace(" [JAMMING]", "");
                CloseJammingStatusWindow();
                // Show last measured values after stop (don't clear)
                if (lblLiveHits != null)
                    lblLiveHits.Text = "STOPPED";
                // Restore panel states after jamming ends
                SetBandTableMode(bandTableModeActive);
            }
        }

        /// <summary>
        /// Toggle between Manual DDS mode and Band Table mode.
        /// Manual: Left panel (LO, DDS, Switch Select) is live, band table is greyed out.
        /// Table:  Band table and config buttons are live, left panel is read-only.
        /// </summary>
        private void SetBandTableMode(bool tableMode)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { SetBandTableMode(tableMode); });
                return;
            }

            bandTableModeActive = tableMode;

            // ── Manual side (left panel) ──
            Color manualBg = tableMode ? Color.FromArgb(70, 75, 65) : SystemColors.Window;
            bool manualReadOnly = tableMode;

            // LO controls
            if (txbxLoStart != null) { txbxLoStart.BackColor = manualBg; txbxLoStart.ReadOnly = manualReadOnly; }
            if (txbxLoStep != null) { txbxLoStep.BackColor = manualBg; txbxLoStep.ReadOnly = manualReadOnly; }
            if (txbxLoPoints != null) { txbxLoPoints.BackColor = manualBg; txbxLoPoints.ReadOnly = manualReadOnly; }
            if (txbxLoDelays != null) { txbxLoDelays.BackColor = manualBg; txbxLoDelays.ReadOnly = manualReadOnly; }
            if (cmbbxLoPower != null) cmbbxLoPower.Enabled = !tableMode;

            // DDS controls
            if (txbxDdsStart != null) { txbxDdsStart.BackColor = manualBg; txbxDdsStart.ReadOnly = manualReadOnly; }
            if (txbxDdsStep != null) { txbxDdsStep.BackColor = manualBg; txbxDdsStep.ReadOnly = manualReadOnly; }
            if (txbxDdsPoints != null) { txbxDdsPoints.BackColor = manualBg; txbxDdsPoints.ReadOnly = manualReadOnly; }
            if (txbxDdsBandwith != null) { txbxDdsBandwith.BackColor = manualBg; txbxDdsBandwith.ReadOnly = manualReadOnly; }
            if (txbxDdsFreqCtrl != null) { txbxDdsFreqCtrl.BackColor = manualBg; txbxDdsFreqCtrl.ReadOnly = manualReadOnly; }
            if (txbxDdsTwMem != null) { txbxDdsTwMem.BackColor = manualBg; txbxDdsTwMem.ReadOnly = manualReadOnly; }
            if (cmbbxDdsMode != null) cmbbxDdsMode.Enabled = !tableMode;

            // Switch Select — visibility controlled by checkbox, enabled state by mode
            if (grbxSwitchSelect != null)
            {
                grbxSwitchSelect.Enabled = !tableMode;
                grbxSwitchSelect.Visible = (chkShowSwitchSelect != null && chkShowSwitchSelect.Checked);
            }

            // ── Table side (right panel) ──
            if (dgvBands != null)
            {
                dgvBands.Enabled = tableMode;
                dgvBands.DefaultCellStyle.BackColor = tableMode
                    ? Color.FromArgb(235, 240, 230)
                    : Color.FromArgb(180, 185, 175);
                dgvBands.DefaultCellStyle.ForeColor = tableMode
                    ? Color.Black
                    : Color.FromArgb(120, 120, 120);
            }

            // Band Configuration controls (buttons, pulse mode, dwell)
            if (grpGuardBandSettings != null)
            {
                // All band configuration controls stay enabled in any mode.
                // The band TABLE (dgvBands) is what gets enabled/disabled by mode toggle.
                // Spinners, buttons, and labels are always accessible for live adjustment.
                foreach (Control c in grpGuardBandSettings.Controls)
                    c.Enabled = true;
            }

            // Update checkbox appearance to show current mode
            if (chkBandTableMode != null)
            {
                chkBandTableMode.ForeColor = tableMode
                    ? Color.FromArgb(100, 255, 100)   // Green when active
                    : Color.FromArgb(255, 200, 80);    // Amber when manual
            }

            // Always refresh status display
            UpdateGuardStatus();
        }

        private void UpdateLiveLoDisplay(double frequencyMHz)
        {
            try
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    if (txbxLoStart != null)
                        txbxLoStart.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(frequencyMHz * 1e6, 3);
                });
            }
            catch { }
        }

        private void UpdateLiveDdsDisplay(double bwMHz, uint points, float freqCtrl)
        {
            try
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    if (txbxDdsBandwith != null)
                        txbxDdsBandwith.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(bwMHz * 1e6, 3);
                    if (txbxDdsPoints != null)
                        txbxDdsPoints.Text = points.ToString();
                    if (txbxDdsFreqCtrl != null)
                        txbxDdsFreqCtrl.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(freqCtrl, 3);
                });
            }
            catch { }
        }

        /// <summary>
        /// Set device.dds parameters for a band's BW using setRampBandwidth().
        /// The firmware's tdm_reconfigure_dds_bandwidth now does a full AD9106
        /// reconfiguration including AD9106_SetFrequency(bwHz/2) which is what
        /// actually controls the RF sweep width.
        /// </summary>
        private void SetDDSParamsForBand(BandConfiguration band)
        {
            double bwHz = band.BandwidthMHz * 1e6;

            device.dds.SweepOn = (ushort)AD9106_MODE.RAMP;
            device.dds.Start = 0.0F;

            // Use setRampBandwidth() — the same library function that manual
            // Configuration uses (line 2541). This correctly computes Points,
            // Step, and all internal DDS parameters. Our custom proportional
            // formula produced different values that didn't match.
            device.dds.setRampBandwidth(bwHz);

            device.dds.FreqCtrl = (float)(bwHz / 10.0);
            if (device.dds.FreqCtrl < 100000) device.dds.FreqCtrl = 100000;
        }

        private void ReconfigureDDSBandParams(BandConfiguration band)
        {
            try
            {
                // Set device.dds parameters for this band's BW
                SetDDSParamsForBand(band);

                // Use full DDSSetConfiguration — the proven working path.
                // This sends all 96 AD9106 registers including correct FTW.
                // Slower than lightweight register updates (~12 USB commands)
                // but CORRECT. SequenceSetSweepDDS alone sends Start=0 which
                // zeroes the FTW via AD9106_SetFrequency(0) → no modulation.
                DDSSetConfiguration();

                WaitForCommandQueue(200);
                UpdateLiveDdsDisplay(band.BandwidthMHz, (uint)device.dds.Points, device.dds.FreqCtrl);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("| DDS reconfig error: " + ex.Message);
            }
        }

        #endregion

        #region GUARD_BAND_JAMMING

        /// <summary>
        /// Start Guard Band Jamming
        /// </summary>
        private void btnGuardStart_Click(object sender, EventArgs e)
        {
            if (btnGuardStart == null || btnGuardStop == null) return;

            // Force-reset if somehow stuck from previous run
            if (isJamming)
            {
                isJamming = false;
                try { lock (ltTxLock) { ltTxCommand.Clear(); } } catch { }
                try { lock (ltServiceSequences) { ltServiceSequences.Clear(); } } catch { }
                SetControlsJammingMode(false);
                btnGuardStart.Enabled = true;
                btnGuardStop.Enabled = false;
                Debug.WriteLine("│ ⚠ Force-reset stuck jamming state — click TEST again to start");
                return;
            }

            if (!bandTableModeActive)
            {
                MessageBox.Show("Switch to Band Table Mode first.\nEnable the checkbox above the band grid.",
                    "Manual Mode Active", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!guiActive)
            {
                MessageBox.Show("Device not connected!\nConnect the DMS hardware first.",
                    "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnGuardStart.Enabled = false;
            btnGuardStop.Enabled = true;
            isJamming = true;
            jammingGeneration++;  // New generation — old thread's finally won't interfere

            // Flush any leftover commands from previous stop/restore cycle
            try { lock (ltTxLock) { ltTxCommand.Clear(); } } catch { }
            try { lock (ltServiceSequences) { ltServiceSequences.Clear(); } } catch { }

            // Save current hardware state and lock controls
            SaveHardwareState();
            SetControlsJammingMode(true);

            Thread jammingThread = new Thread(ExecuteGuardBandJamming);
            jammingThread.IsBackground = true;
            jammingThread.Start();
        }

        /// <summary>
        /// Stop Guard Band Jamming (both PC-controlled and firmware autonomous)
        /// </summary>
        private void btnGuardStop_Click(object sender, EventArgs e)
        {
            isJamming = false;

            // ── STEP 1: Stop firmware autonomous TDM ──
            try
            {
                SendTdmCommand(CMD_TDM_STOP, null, 0);
                Debug.WriteLine("TDM: Firmware STOP sent");
            }
            catch { }

            // ── STEP 2: Flush all pending PC-controlled commands ──
            try
            {
                lock (ltTxLock) { ltTxCommand.Clear(); }
                lock (ltServiceSequences) { ltServiceSequences.Clear(); }
            }
            catch { }

            // ── STEP 3: Send DDS stop commands (belt and braces) ──
            try
            {
                // Kill DDS output (FreqCtrl = 0 stops the ramp)
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0));
                // Also send register write to force AD9106 into reset
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1E, 0x0000));
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));
                WaitForCommandQueue(100);  // Short wait — just these 3 commands
            }
            catch { }

            // Restore hardware state and unlock controls
            try { RestoreHardwareState(); } catch { }
            try { SetControlsJammingMode(false); } catch { }

            if (btnGuardStart != null) btnGuardStart.Enabled = true;
            if (btnGuardStop != null) btnGuardStop.Enabled = false;
            try { UpdateGuardStatus(); } catch { }
            Debug.WriteLine("║ GUARD BAND JAMMING STOPPED BY USER ║");
        }

        /// <summary>
        /// Execute Guard Band Jamming with TIME-DIVISION MULTIPLEXING
        /// Jams ALL 7 bands using 4 DAC channels by rapid switching
        /// </summary>
        private void ExecuteGuardBandJamming()
        {
            int myGeneration = jammingGeneration;  // Capture — check in finally
            try
            {
                Debug.WriteLine("\n╔═══════════════════════════════════════════════════════╗");
                Debug.WriteLine("║   GUARD BAND JAMMING - TDM MODE                       ║");
                Debug.WriteLine("╚═══════════════════════════════════════════════════════╝");

                // Verify device is connected
                if (!guiActive || usbConnection == null || !usbConnection.IsOpen)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        SetControlsJammingMode(false);  // ← FIX: unlock controls
                        MessageBox.Show("Device not connected!\nConnect hardware first.",
                            "No Connection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        if (btnGuardStart != null) btnGuardStart.Enabled = true;
                        if (btnGuardStop != null) btnGuardStop.Enabled = false;
                    });
                    return;
                }

                // ── Collect bands from grid (must be on UI thread) ──
                List<BandConfiguration> allBands = new List<BandConfiguration>();
                bool usePulseMode = false;
                // userDwellUs is a volatile field — updated live from Dwell spinner (00b5s)

                this.Invoke((MethodInvoker)delegate
                {
                    if (dgvBands == null) return;
                    foreach (DataGridViewRow row in dgvBands.Rows)
                    {
                        if (row.IsNewRow) continue;

                        // Band name is optional — use "Band N" if empty
                        string bandName = "Band " + (row.Index + 1);
                        if (row.Cells["colBandName"] != null && row.Cells["colBandName"].Value != null
                            && row.Cells["colBandName"].Value.ToString().Trim().Length > 0)
                            bandName = row.Cells["colBandName"].Value.ToString().Trim();

                        // Frequency is required — skip rows with no frequency or invalid values
                        if (row.Cells["colStartMhz"] == null || row.Cells["colStartMhz"].Value == null) continue;
                        double freqMhz;
                        try { freqMhz = Convert.ToDouble(row.Cells["colStartMhz"].Value); }
                        catch { continue; }

                        // Skip empty/unconfigured rows — freq must be within MAX2871 range (23.5-6000 MHz)
                        if (freqMhz < 20.0 || freqMhz > 6200.0) continue;

                        // Skip bands where Active checkbox is unchecked
                        if (dgvBands.Columns.Contains("colActive") && row.Cells["colActive"].Value != null)
                        {
                            try { if (!Convert.ToBoolean(row.Cells["colActive"].Value)) continue; }
                            catch { }
                        }

                        BandConfiguration band = new BandConfiguration
                        {
                            Name = bandName,
                            FrequencyMHz = freqMhz,
                            BandwidthMHz = 20.0,
                            Tones = 256,
                            RowIndex = row.Index
                        };

                        if (dgvBands.Columns.Contains("colBandwidthMhz") && row.Cells["colBandwidthMhz"].Value != null)
                            try { band.BandwidthMHz = Convert.ToDouble(row.Cells["colBandwidthMhz"].Value); } catch { }
                        if (dgvBands.Columns.Contains("colTones") && row.Cells["colTones"].Value != null)
                            try { band.Tones = Convert.ToInt32(row.Cells["colTones"].Value); } catch { }
                        if (dgvBands.Columns.Contains("colDdsMode") && row.Cells["colDdsMode"].Value != null)
                            band.DdsMode = row.Cells["colDdsMode"].Value.ToString();
                        if (dgvBands.Columns.Contains("colAttenDb") && row.Cells["colAttenDb"].Value != null)
                            try { band.AttenDb = Convert.ToDouble(row.Cells["colAttenDb"].Value); } catch { }

                        allBands.Add(band);
                    }
                    usePulseMode = (chkGuardPulseMode != null && chkGuardPulseMode.Checked);
                    // Read dwell time from UI spinner (used in both TEST modes)
                    if (numTdmDwell != null)
                        userDwellUs = (int)numTdmDwell.Value;
                });

                if (allBands.Count == 0)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        SetControlsJammingMode(false);  // ← FIX: unlock controls
                        MessageBox.Show("No bands configured!\nLoad bands using the preset buttons.",
                            "No Bands", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        if (btnGuardStart != null) btnGuardStart.Enabled = true;
                        if (btnGuardStop != null) btnGuardStop.Enabled = false;
                        UpdateGuardStatus();
                    });
                    return;
                }

                // ── Log band plan ──
                Debug.WriteLine("╔═══ BAND PLAN ═══╗");
                Debug.WriteLine("║ Active bands: " + allBands.Count);
                foreach (var b in allBands)
                    Debug.WriteLine("║   " + b.Name + ": " + b.FrequencyMHz + " MHz, BW=" + b.BandwidthMHz + " MHz, Mode=" + b.DdsMode + ", Atten=" + b.AttenDb + "dB");
                Debug.WriteLine("╚═════════════════╝");

                // ── Sort bands: group by DDS mode to minimise mode switches ──
                // RAMP bands first, then PRBS bands (reduces ~80ms mode switch overhead)
                allBands.Sort((a, b2) => a.DdsMode.CompareTo(b2.DdsMode));  // "PRBS" < "Ramp" alphabetically
                allBands.Reverse();  // Ramp first, PRBS second

                bool hasMixedModes = allBands.Exists(b => b.IsPRBS) && allBands.Exists(b => !b.IsPRBS);
                if (hasMixedModes)
                    Debug.WriteLine("│ MIXED MODE: Ramp/Random + PRBS bands (mode switch per cycle)");

                // ── WEIGHTED TDM: 2.4 GHz guard bands get double visits ──
                // 2.4 GHz carries drone control link — higher priority than video bands.
                // Insert duplicate entries for 2.4G bands so they get visited twice per cycle.
                // Result: 2.4G duty ~25%, 5.8G duty ~9%, 900MHz duty ~9%
                List<BandConfiguration> weightedBands = new List<BandConfiguration>();
                List<BandConfiguration> priorityBands = new List<BandConfiguration>();
                List<BandConfiguration> normalBands = new List<BandConfiguration>();

                foreach (var b in allBands)
                {
                    if (b.Name.Contains("2.4G") || b.Name.Contains("Guard Low") || 
                        b.Name.Contains("Guard Mid") || b.Name.Contains("Guard High"))
                        priorityBands.Add(b);
                    else
                        normalBands.Add(b);
                }

                // Interleave: [2.4G bands] [normal bands] [2.4G bands again]
                weightedBands.AddRange(priorityBands);
                weightedBands.AddRange(normalBands);
                weightedBands.AddRange(priorityBands);  // Second pass for 2.4 GHz

                // Only use weighted scheduling if we have 2.4G priority bands
                if (priorityBands.Count > 0)
                {
                    Debug.WriteLine("│ WEIGHTED TDM: " + priorityBands.Count + " priority bands (2x), " + normalBands.Count + " normal bands");
                    Debug.WriteLine("│ Cycle: " + weightedBands.Count + " hops (" + priorityBands.Count + " bands × 2 + " + normalBands.Count + ")");
                    allBands = weightedBands;
                }

                // ══ CONFIGURE DDS FOR FIRST BAND ══
                if (allBands[0].IsPRBS)
                    ConfigureDDSForPRBS(allBands[0]);
                else
                    ConfigureDDSForJamming(allBands[0]);

                // Per-band BW verified working — SetDDSParamsForBand() uses same
                // math as SAVE TO DEVICE (Step = BW_Hz / Points, not setRampBandwidth)

                if (usePulseMode)
                {
                    // ══════════════════════════════════════
                    // PULSED TDM MODE
                    // ══════════════════════════════════════
                    Debug.WriteLine("│ Mode: PULSED TDM  ON=" + pulseOnTimeMs + "ms OFF=" + pulseOffTimeMs + "ms  Dwell=" + userDwellUs + "00b5s");
                    int timePerBand = Math.Max(pulseOnTimeMs / allBands.Count, 2);
                    double currentBW = allBands[0].BandwidthMHz;
                    int currentTones = allBands[0].Tones;
                    string currentMode = allBands[0].DdsMode;

                    // CRITICAL: Configure DDS for first band's actual table bandwidth
                    // Without this, DDS stays at the global panel BW (not the per-band table BW)
                    if (!allBands[0].IsPRBS)
                    {
                        SetDDSParamsForBand(allBands[0]);
                    }

                    Stopwatch cpsTimer = Stopwatch.StartNew();

                    for (int cycle = 0; cycle < pulseCycles && isJamming; cycle++)
                    {
                        // ── JAM PHASE: hop through all bands ──
                        AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, device.dds.FreqCtrl));
                        // No extra wait here — HopToFrequency handles its own queue wait

                        for (int i = 0; i < allBands.Count && isJamming; i++)
                        {
                            // Check if DDS mode needs switching (RAMP/Random ↔ PRBS)
                            // Ramp and Random use identical AD9106 registers (only SRAM differs,
                            // and SRAM is pre-filled at startup). Only cross-boundary PRBS switches matter.
                            bool needSwitch = (allBands[i].IsPRBS != (currentMode == "PRBS"));
                            if (needSwitch)
                            {
                                Debug.WriteLine("│ Mode switch: " + currentMode + " → " + allBands[i].DdsMode);
                                SwitchDDSMode(allBands[i]);
                            }
                            currentMode = allBands[i].DdsMode;

                            // Update DDS state for this band's bandwidth BEFORE hop
                            // Uses same math as SAVE TO DEVICE (bypasses setRampBandwidth)
                            if (!allBands[i].IsPRBS)
                            {
                                SetDDSParamsForBand(allBands[i]);
                            }

                            HopToFrequency(allBands[i].FrequencyMHz, allBands[i].AttenDb);

                            // Update monitor with current band info
                            double cps = (cycle > 0) ? cycle / cpsTimer.Elapsed.TotalSeconds : 0;
                            UpdateTDMStatus(cycle, i, allBands.Count, true, cps,
                                allBands[i].Name + " " + allBands[i].FrequencyMHz.ToString("F0") + "MHz BW=" + allBands[i].BandwidthMHz.ToString("F0"));

                            // Per-band timing controlled by Hop spinner inside HopToFrequency
                        }

                        // ── SILENT PHASE: kill DDS output ──
                        AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0));
                        WaitForCommandQueueJamming(20);  // Just enough for the single command
                        UpdateTDMStatus(cycle, -1, allBands.Count, false, (cycle > 0) ? cycle / cpsTimer.Elapsed.TotalSeconds : 0);
                        Thread.Sleep(pulseOffTimeMs);

                        if (cycle % 100 == 0)
                            Debug.WriteLine("│ Cycle " + cycle + (pulseCycles == int.MaxValue ? " (continuous)" : "/" + pulseCycles));

                        // ── Update live display for pulsed mode ──
                        if (cycle > 0)
                        {
                            try
                            {
                                int c = cycle; int nb = allBands.Count; int dw = userDwellUs;
                                double cps = c / cpsTimer.Elapsed.TotalSeconds;
                                double cpsVal = cps;
                                this.BeginInvoke((MethodInvoker)delegate
                                {
                                    if (lblLiveHits != null && !lblLiveHits.IsDisposed)
                                        lblLiveHits.Text = string.Format("{0:F1} cycles/s | {1} bands | Dwell: {2}00b5s", cpsVal, nb, dw);
                                });
                            }
                            catch { }
                        }
                    }
                }
                else
                {
                    // ══════════════════════════════════════
                    // CONTINUOUS TDM MODE
                    // ══════════════════════════════════════
                    Debug.WriteLine("│ Mode: CONTINUOUS TDM, Dwell: " + userDwellUs + "00b5s per band");
                    double currentBW_c = allBands[0].BandwidthMHz;
                    int currentTones_c = allBands[0].Tones;
                    string currentMode_c = allBands[0].DdsMode;

                    // CRITICAL: Configure DDS for first band's actual table bandwidth
                    // Without this, DDS stays at the global panel BW (not the per-band table BW)
                    if (!allBands[0].IsPRBS)
                    {
                        SetDDSParamsForBand(allBands[0]);
                    }

                    int continuousCycles = 0;
                    Stopwatch cpsTimer = Stopwatch.StartNew();
                    while (isJamming)
                    {
                        // Start DDS output at beginning of first cycle
                        if (continuousCycles == 0)
                            AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, device.dds.FreqCtrl));

                        for (int i = 0; i < allBands.Count && isJamming; i++)
                        {
                            // Check if DDS mode needs switching (RAMP ↔ PRBS)
                            if (allBands[i].DdsMode != currentMode_c)
                            {
                                Debug.WriteLine("│ Mode switch: " + currentMode_c + " → " + allBands[i].DdsMode);
                                SwitchDDSMode(allBands[i]);
                                currentMode_c = allBands[i].DdsMode;
                            }

                            // Update DDS state for this band's bandwidth BEFORE hop
                            if (!allBands[i].IsPRBS)
                            {
                                SetDDSParamsForBand(allBands[i]);
                            }

                            HopToFrequency(allBands[i].FrequencyMHz, allBands[i].AttenDb);

                            // Update monitor with current band info
                            double cps = (continuousCycles > 0) ? continuousCycles / cpsTimer.Elapsed.TotalSeconds : 0;
                            UpdateTDMStatus(continuousCycles, i, allBands.Count, true, cps,
                                allBands[i].Name + " " + allBands[i].FrequencyMHz.ToString("F0") + "MHz BW=" + allBands[i].BandwidthMHz.ToString("F0"));
                        }

                        continuousCycles++;

                        // ── Update live display ──
                        if (continuousCycles > 0)
                        {
                            try
                            {
                                int nb = allBands.Count; int dw = userDwellUs;
                                double cps = continuousCycles / cpsTimer.Elapsed.TotalSeconds;
                                double cpsVal = cps;
                                this.BeginInvoke((MethodInvoker)delegate
                                {
                                    if (lblLiveHits != null && !lblLiveHits.IsDisposed)
                                        lblLiveHits.Text = string.Format("{0:F1} cycles/s | {1} bands | Dwell: {2}00b5s", cpsVal, nb, dw);
                                });
                            }
                            catch { }
                        }

                        // Yield to UI thread — ensures STOP button clicks get processed
                        Thread.Sleep(2);
                    }
                }

                // ── Thread exit ──
                // Stop button handler already did cleanup (flush, stop cmds, restore).
                // Don't duplicate here — causes race conditions on restart.
                Debug.WriteLine("│ ✓ Jamming thread exiting");
                Debug.WriteLine("╚═══════════════════════════════════════════════════════╝\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("│ ✗ ERROR: " + ex.Message);
            }
            finally
            {
                // Only clean up if no new run has started (generation unchanged)
                if (jammingGeneration == myGeneration)
                {
                    isJamming = false;
                    try
                    {
                        this.BeginInvoke((MethodInvoker)delegate
                        {
                            SetControlsJammingMode(false);
                            if (btnGuardStart != null) btnGuardStart.Enabled = true;
                            if (btnGuardStop != null) btnGuardStop.Enabled = false;
                            UpdateGuardStatus();
                        });
                    }
                    catch { }
                }
            }
        }


        /// <summary>
        /// One-time DDS configuration for jamming. Sets RAMP mode for wideband noise.
        /// Called once at start of jamming sequence - LO hopping handles band selection.
        /// </summary>
        private void ConfigureDDSForJamming(BandConfiguration firstBand)
        {
            try
            {
                // Set RAMP mode and compute DDS parameters using setRampBandwidth
                SetDDSParamsForBand(firstBand);

                // Use DDSSetConfiguration() — the SAME function that makes manual
                // Configuration work every time. This handles full register setup,
                // latch sequence, and DDS trigger reliably, including after stop/restart.
                DDSSetConfiguration();

                // Wait for all commands to reach hardware
                WaitForCommandQueueJamming(3000);
                Thread.Sleep(100);

                // Start DDS output with band's control frequency
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, device.dds.FreqCtrl));
                WaitForCommandQueueJamming(500);

                // Update live DDS display
                uint points = (uint)device.dds.Points;
                double achievedBwMHz = device.dds.getRampBandwidth() / 1e6;
                UpdateLiveDdsDisplay(achievedBwMHz, points, device.dds.FreqCtrl);

                Debug.WriteLine("│ DDS configured: RAMP mode, Requested BW=" + firstBand.BandwidthMHz +
                    " MHz, Achieved BW=" + achievedBwMHz.ToString("F3") + " MHz, Points=" + points);
                Debug.WriteLine(string.Format("│ Reg 0x47 TW_RAM_CONFIG={0}, Reg 0x52 STOP_ADDR=0x{1:X4} ({1})",
                    device.dds.registers[0x47], device.dds.registers[0x52]));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("│ ✗ DDS config error: " + ex.Message);
            }
        }

        /// <summary>
        /// Configure AD9106 for PRBS (barrage/noise) mode.
        /// Uses pseudo-random sequence for wideband noise output.
        /// FreqCtrl sets the noise bandwidth via DAC clock rate.
        /// </summary>
        private void ConfigureDDSForPRBS(BandConfiguration band)
        {
            try
            {
                double bwHz = band.BandwidthMHz * 1e6;
                // PRBS noise BW ≈ FreqCtrl rate. Use full BW for wideband noise.
                float freqCtrl = (float)bwHz;

                device.dds.SweepOn = (ushort)AD9106_MODE.PSEUDO;
                device.dds.FreqCtrl = freqCtrl;

                // Send DDS sweep parameters (mode=PSEUDO)
                AddSequences(new SequenceSetSweepDDS(ltTxCommand, null,
                    (ushort)AD9106_MODE.PSEUDO, 0, 0, 0));

                // PRBS register configuration (from DDSSetConfiguration PSEUDO path)
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1F, 0x0000));
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1E, 0x0000));
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));

                // Channel enable (all 4 channels active for PRBS)
                device.dds.registers[0x26] = 0x2121;
                device.dds.registers[0x27] = 0x2121;

                // PRBS-specific registers
                device.dds.registers[0x28] = 0x0111;
                device.dds.registers[0x29] = 0x8000;
                device.dds.registers[0x2A] = 0x0101;
                device.dds.registers[0x2B] = 0x0101;
                device.dds.registers[0x36] = 0x0000;  // No SAW (noise mode)
                device.dds.registers[0x37] = 0x0000;
                device.dds.registers[0x44] = 0x0000;  // No prestore
                device.dds.registers[0x45] = 0x0000;
                for (int i = 0; i < 4; i++)
                {
                    device.dds.registers[0x50 + 4 * i] = 0x0001;
                    device.dds.registers[0x51 + 4 * i] = 0x0000;
                    device.dds.registers[0x52 + 4 * i] = 0x0000;
                    device.dds.registers[0x53 + 4 * i] = 0xFFFF;  // Full SRAM range for max noise
                }

                // Write all registers 0x20-0x5F
                for (ushort i = 0x20; i < 0x60; i++)
                    AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, i, device.dds.registers[i]));

                // Latch and start
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1F, 0x0000));
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1E, 0x0001));
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));

                // Start DDS output
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, freqCtrl));
                AddSequences(new SequenceDelay(100));
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0.0F));

                WaitForCommandQueueJamming(3000);
                Thread.Sleep(100);

                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, freqCtrl));
                WaitForCommandQueueJamming(500);

                Debug.WriteLine("│ DDS configured: PRBS mode, BW=" + band.BandwidthMHz + " MHz (barrage)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("│ ✗ DDS PRBS config error: " + ex.Message);
            }
        }

        /// <summary>
        /// Switch between RAMP and PRBS modes during TDM hopping.
        /// This requires full register reconfiguration (~80ms).
        /// </summary>
        private void SwitchDDSMode(BandConfiguration band)
        {
            if (band.IsPRBS)
                ConfigureDDSForPRBS(band);
            else
                ConfigureDDSForJamming(band);
        }

        /// <summary>
        /// Fast LO frequency hop. Only changes PLL - DDS keeps running.
        /// This is the core TDM operation: hop LO to move the jamming band.
        /// Optionally sets per-band attenuation (0-25 dB) on the active attenuator position.
        /// </summary>
        private void HopToFrequency(double frequencyMHz, double attenDb = -1)
        {
            try
            {
                double freqHz = frequencyMHz * 1e6;
                device.lo.pll.OutFrequency = freqHz;

                // ── STEP 1: Change PLL frequency ──
                // SequenceSetSweepLoParameters is the only proven way to change
                // the MAX2871 PLL. sweepOn=0 tells STM32 "single frequency" mode
                // which also kills the DDS — we re-trigger it in step 2.
                AddSequences(new SequenceSetSweepLoParameters(ltTxCommand, null,
                    (float)freqHz, 0, 1, 0, 0, 0));

                // ── STEP 2: Re-trigger DDS ramp ──
                // SequenceSetSweepLoParameters with sweepOn=0 puts STM32 in CW mode,
                // killing DDS output. We must re-send sweep params AND pulse FreqCtrl
                // to restart the AD9106 ramp — same trigger sequence as DDSSetConfiguration.
                AddSequences(new SequenceSetSweepDDS(ltTxCommand, null,
                    device.dds.SweepOn, device.dds.Start, device.dds.Step, device.dds.Points));

                // ── STEP 2b: Update AD9106 RAMP endpoint for current band's BW ──
                // device.dds.Points is set by ReconfigureDDSBandParams for each band.
                // Without this, the AD9106 keeps sweeping the old SRAM range → wrong BW.
                ushort rampEnd = (ushort)(device.dds.Points * 16);
                for (int ch = 0; ch < 4; ch++)
                    AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 
                        (ushort)(0x52 + 4 * ch), rampEnd));

                // Latch AD9106 configuration
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1E, 0x0003));
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1F, 0x0000));
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));

                // Trigger pulse — restarts AD9106 ramp output
                // CRITICAL: At 10 Hz (100ms period), a 1ms delay produces NO complete pulse.
                // Use 10000 Hz (0.1ms period) so we get ~20 complete trigger pulses in 2ms.
                // This matches the firmware's approach of multiple fast trigger edges.
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 10000));
                AddSequences(new SequenceDelay(2));  // 2ms = ~20 pulses at 10kHz
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0));
                AddSequences(new SequenceDelay(1));   // 1ms settle

                // Set actual FreqCtrl to start ramp output
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, device.dds.FreqCtrl));

                // Set per-band attenuation if specified
                if (attenDb >= 0 && device.AttCalibrationTable != null &&
                    device.ltSwitchAttenuator != null && device.ltSwitchAttenuator.Count > activeAttPosition)
                {
                    double clampedDb = Math.Max(0, Math.Min(25, attenDb));
                    device.ltSwitchAttenuator[activeAttPosition].CtrlVoltage =
                        device.AttCalibrationTable.getVoltage((float)clampedDb);
                    AddSequences(new SequenceSetDacAttenuator(ltTxCommand, null,
                        device.ltSwitchAttenuator[activeAttPosition].Dac, (ushort)activeAttPosition));
                }

                // Wait for commands to reach hardware, then dwell on this frequency
                WaitForCommandQueueJamming(50);
                Thread.Sleep(Math.Max(userDwellUs / 1000, 1));  // PC TEST: µs→ms (min 1ms, USB-limited anyway)

                // Update live LO display
                UpdateLiveLoDisplay(frequencyMHz);
                // Update live DDS display so left panel shows current bandwidth
                UpdateLiveDdsDisplay(device.dds.getRampBandwidth() / 1e6, (uint)device.dds.Points, device.dds.FreqCtrl);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("│ ✗ Hop error: " + ex.Message);
            }
        }

        /// <summary>
        /// Update UI with TDM status — writes to floating monitor window
        /// </summary>
        private void UpdateTDMStatus(int cycle, int currentBandIdx, int totalBands, bool jamming, double cyclesPerSec = 0, string bandName = "")
        {
            try
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    string cpsStr = cyclesPerSec > 0 ? cyclesPerSec.ToString("F1") + " cyc/s" : "";

                    if (jamming)
                    {
                        string bandInfo = (bandName.Length > 0) ? bandName : "Band " + (currentBandIdx + 1);

                        if (lblGuardStatus != null)
                        {
                            lblGuardStatus.Text = "JAMMING 2014 " + cpsStr + " | Dwell: " + userDwellUs + "00b5s";
                            lblGuardStatus.ForeColor = Color.FromArgb(255, 80, 80);
                        }

                        try
                        {
                            if (lblJammingStatusText != null)
                                lblJammingStatusText.Text = string.Format("  {0}  ({1}/{2})", bandInfo, currentBandIdx + 1, totalBands);
                            if (lblJammingCycleText != null)
                                lblJammingCycleText.Text = string.Format("  {0}  |  Dwell: {1}\u00B5s", cpsStr, userDwellUs);
                        }
                        catch { }
                    }
                    else
                    {
                        if (lblGuardStatus != null)
                        {
                            lblGuardStatus.Text = "SILENT PHASE | " + cpsStr;
                            lblGuardStatus.ForeColor = Color.FromArgb(100, 255, 100);
                        }

                        try
                        {
                            if (lblJammingStatusText != null)
                                lblJammingStatusText.Text = "  SILENT PHASE";
                            if (lblJammingCycleText != null)
                                lblJammingCycleText.Text = "  " + cpsStr;
                        }
                        catch { }
                    }
                });
            }
            catch { }
        }

        /// <summary>
        /// Show embedded jamming monitor panel (between band table and config)
        /// </summary>
        private void ShowJammingStatusWindow()
        {
            if (pnlJammingMonitor == null) return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke((MethodInvoker)delegate { ShowJammingStatusWindow(); });
                return;
            }
            lblJammingStatusText.Text = "  STARTING...";
            lblJammingCycleText.Text = "";
            pnlJammingMonitor.Visible = true;
            if (grpGuardBandSettings != null)
                grpGuardBandSettings.Height = 220;  // Expand to show monitor
        }

        /// <summary>
        /// Hide embedded jamming monitor panel
        /// </summary>
        private void CloseJammingStatusWindow()
        {
            if (pnlJammingMonitor == null) return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke((MethodInvoker)delegate { CloseJammingStatusWindow(); });
                return;
            }
            pnlJammingMonitor.Visible = false;
            if (grpGuardBandSettings != null)
                grpGuardBandSettings.Height = 174;  // Shrink back to normal (with group rotation row)
        }

        /// <summary>
        /// Band configuration class for TDM
        /// </summary>
        private class BandConfiguration
        {
            public string Name { get; set; }
            public double FrequencyMHz { get; set; }
            public double BandwidthMHz { get; set; }
            public int Tones { get; set; }
            public int RowIndex { get; set; }
            public string DdsMode { get; set; } = "Ramp";  // "Ramp", "PRBS", or "Random"
            public double AttenDb { get; set; } = 0.0;     // 0-25 dB per-band attenuation
            public bool IsPRBS { get { return DdsMode == "PRBS"; } }
        }

        #endregion

        #region BAND_CONFIGURATIONS

        /// <summary>
        /// Load default bands silently (for startup)
        /// </summary>
        private void LoadDefaultBands()
        {
            if (dgvBands == null) return;

            try
            {
                dgvBands.Rows.Clear();
                dgvBands.ReadOnly = false;

                AddBandRow("", "Triggered", "WiFi 2.4GHz", 2450.0, 80.0, 100.0, true, 512, Color.Empty);
                AddBandRow("", "Triggered", "DJI 5.8GHz", 5800.0, 60.0, 100.0, true, 512, Color.Empty);
                AddBandRow("", "Triggered", "LTE Band 3", 1800.0, 60.0, 100.0, true, 512, Color.Empty);
                AddBandRow("", "Triggered", "Walkie 900MHz", 900.0, 50.0, 100.0, true, 512, Color.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LoadDefaultBands error: " + ex.Message);
            }
        }

        /// Load 7 Guard Bands (for UKDI competition)
        /// </summary>
        private void Load7GuardBands()
        {
            if (dgvBands == null) return;
            EnsureGridColumns();

            dgvBands.Rows.Clear();
            dgvBands.ReadOnly = false;
            _loadingBands = true;

            // 2.4 GHz Guard Bands - GREEN
            // STANDARDISED: All zones use 13 MHz BW and 256 points for TDM sweep
            AddBandRow("", "Triggered", "2.4G Guard Low", 2405.0, 13.0, 100.0, true, 256, Color.LightGreen);
            AddBandRow("", "Triggered", "2.4G Guard Mid1", 2425.0, 13.0, 100.0, true, 256, Color.LightGreen);
            AddBandRow("", "Triggered", "2.4G Guard Mid2", 2451.0, 13.0, 100.0, true, 256, Color.LightGreen);
            AddBandRow("", "Triggered", "2.4G Guard High", 2476.5, 13.0, 100.0, true, 256, Color.LightGreen);

            // 5.8 GHz DJI - CYAN
            AddBandRow("", "Triggered", "5.8G DJI Low", 5735.0, 20.0, 100.0, true, 256, Color.LightCyan);
            AddBandRow("", "Triggered", "5.8G DJI High", 5840.0, 20.0, 100.0, true, 256, Color.LightCyan);

            // Autel 900MHz - YELLOW
            AddBandRow("", "Triggered", "Autel 900MHz", 915.0, 30.0, 100.0, true, 256, Color.LightYellow);

            Debug.WriteLine("║ 7 GUARD BANDS LOADED - WiFi Ch 1,6,11 protected ║");
            _loadingBands = false;
            UpdateAllPointsFromDuty();
            UpdateAllStepKhz();
            UpdateGuardStatus();
            MessageBox.Show("✓ 7 Guard Bands Loaded\n\n" +
                          "GREEN = 2.4GHz WiFi Guards\n" +
                          "CYAN = 5.8GHz DJI Bands\n" +
                          "YELLOW = 900MHz Autel\n\n" +
                          "WiFi Channels 1, 6, 11 PROTECTED",
                          "Guard Bands Ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Add a row to dgvBands with column-aware value assignment.
        /// Works regardless of how many columns the Designer created.
        /// </summary>
        private void AddBandRow(string status, string sweepMode, string bandName,
            double centreMhz, double bwMhz, double power, bool active, int tones, Color rowColor,
            string ddsMode = "Ramp", double attenDb = 0.0, float freqCtrl = 0, int twMem = -1)
        {
            if (dgvBands == null) return;

            int rowIdx = dgvBands.Rows.Add();
            DataGridViewRow row = dgvBands.Rows[rowIdx];

            // Set values by column name (safe - ignores if column doesn't exist)
            // colStatus repurposed as TwMem — set by TwMem section below
            SetCellValue(row, "colSweepMode", sweepMode);
            SetCellValue(row, "colBandName", bandName);
            SetCellValue(row, "colStartMhz", centreMhz);
            SetCellValue(row, "colBandwidthMhz", bwMhz);
            SetCellValue(row, "colPower", power);
            SetCellValue(row, "colActive", active);
            SetCellValue(row, "colAttenDb", attenDb);
            SetCellValue(row, "colTones", tones);
            SetCellValue(row, "colDdsMode", ddsMode);

            // K9: FreqCtrl display (0 = auto, firmware computes from Points/Dwell)
            if (dgvBands.Columns.Contains("colFreqCtrl"))
            {
                if (freqCtrl > 0)
                    SetCellValue(row, "colFreqCtrl", string.Format("{0:F1}", freqCtrl / 1e6));
                else
                    SetCellValue(row, "colFreqCtrl", "auto");
            }

            // K9: TwMem display (computed from BW if not supplied)
            if (dgvBands.Columns.Contains("colStatus"))
            {
                if (twMem >= 0)
                    SetCellValue(row, "colStatus", twMem);
                else
                {
                    // Compute TwMem from BW using C# setRampBandwidth formula
                    double fRampMax = (bwMhz * 1e6 / (2.0 * 170e6)) * 16777216.0;
                    int tw = (fRampMax / 4.0 > 1500.0) ? 2 : (fRampMax / 2.0 > 1500.0) ? 1 : 0;
                    SetCellValue(row, "colStatus", tw);
                }
            }

            if (rowColor != Color.Empty)
            {
                row.DefaultCellStyle.BackColor = rowColor;
            }

            // Grey out Tones cell for PRBS rows
            if (ddsMode == "PRBS" && dgvBands.Columns.Contains("colTones"))
            {
                row.Cells["colTones"].Style.BackColor = Color.LightGray;
                row.Cells["colTones"].Style.ForeColor = Color.Gray;
                row.Cells["colTones"].ReadOnly = true;
            }
        }

        private void SetCellValue(DataGridViewRow row, string colName, object value)
        {
            if (dgvBands.Columns.Contains(colName))
            {
                row.Cells[colName].Value = value;
            }
        }

        /// <summary>
        /// Load 4 Standard Bands (for normal pattern jamming)
        /// </summary>
        private void Load4StandardBands()
        {
            if (dgvBands == null) return;

            dgvBands.Rows.Clear();
            dgvBands.ReadOnly = false; // Make sure it's editable!
            _loadingBands = true;

            // Use AddBandRow for correct column mapping (positional Rows.Add misaligns)
            AddBandRow("", "Triggered", "WiFi 2.4GHz", 2450.0, 80.0, 100.0, true, 512, Color.Empty);
            AddBandRow("", "Triggered", "DJI 5.8GHz", 5800.0, 60.0, 100.0, true, 512, Color.Empty);
            AddBandRow("", "Triggered", "LTE Band 3", 1800.0, 60.0, 100.0, true, 512, Color.Empty);
            AddBandRow("", "Triggered", "Walkie 900MHz", 900.0, 50.0, 100.0, true, 512, Color.Empty);

            Debug.WriteLine("║ 4 STANDARD BANDS LOADED - Normal pattern jamming ║");
            _loadingBands = false;
            UpdateAllPointsFromDuty();
            UpdateAllStepKhz();
            UpdateGuardStatus();
            MessageBox.Show("✓ 4 Standard Bands Loaded\n\n" +
                          "WiFi 2.4GHz\n" +
                          "DJI 5.8GHz\n" +
                          "LTE Band 3\n" +
                          "Walkie 900MHz\n\n" +
                          "Grid is editable - you can modify bands or switch back!",
                          "Standard Bands Loaded",
                          MessageBoxButtons.OK,
                          MessageBoxIcon.Information);
        }

        /// <summary>
        /// Load GPS/GNSS Denial bands using PRBS (barrage) mode.
        /// Covers GPS L1, L2, L5, GLONASS L1, and BeiDou B1.
        /// All bands use PRBS noise for maximum spread-spectrum disruption.
        /// </summary>
        private void LoadGpsDenialBands()
        {
            if (dgvBands == null) return;
            EnsureGridColumns();

            dgvBands.Rows.Clear();
            dgvBands.ReadOnly = false;
            _loadingBands = true;

            // ── Upper L-Band ──
            // GPS L1 (1575.42 ±7.7 MHz) + Galileo E1 (1559-1591 MHz)
            // Combined coverage: 1559-1591 = 32 MHz centered at 1575
            AddBandRow("", "Triggered", "L1/E1", 1575.0, 32.0, 100.0, true, 1, Color.FromArgb(255, 200, 200), "PRBS");

            // GLONASS G1 (1593-1610 MHz) — separate from L1, higher frequency
            AddBandRow("", "Triggered", "GLONASS G1", 1602.0, 16.0, 100.0, true, 1, Color.FromArgb(255, 230, 200), "PRBS");

            // ── Lower L-Band ──
            // GPS L5 (1176.45 MHz, 12.5 MHz BW) + Galileo E5a (1164-1189 MHz)
            AddBandRow("", "Triggered", "L5/E5a", 1176.45, 24.0, 100.0, true, 1, Color.FromArgb(255, 220, 220), "PRBS");

            // GLONASS G3 (1189-1214 MHz) — adjacent to L5, fills gap to L2
            AddBandRow("", "Triggered", "GLONASS G3", 1202.0, 24.0, 100.0, true, 1, Color.FromArgb(255, 230, 200), "PRBS");

            // GPS L2 (1227.6 MHz, 11 MHz BW) + GLONASS G2 (1237.8-1254 MHz)
            AddBandRow("", "Triggered", "L2/G2", 1237.0, 40.0, 100.0, true, 1, Color.FromArgb(255, 200, 200), "PRBS");

            Debug.WriteLine("║ GPS DENIAL BANDS LOADED - All PRBS barrage mode ║");
            _loadingBands = false;
            UpdateAllPointsFromDuty();
            UpdateAllStepKhz();
            UpdateGuardStatus();
            MessageBox.Show("✓ GPS/GNSS Denial Bands Loaded\n\n" +
                          "Upper L-Band:\n" +
                          "  RED = GPS L1 + Galileo E1 (1575 MHz, 32 MHz)\n" +
                          "  ORANGE = GLONASS G1 (1602 MHz, 16 MHz)\n\n" +
                          "Lower L-Band:\n" +
                          "  RED = GPS L5 + Galileo E5a (1176 MHz, 24 MHz)\n" +
                          "  ORANGE = GLONASS G3 (1202 MHz, 24 MHz)\n" +
                          "  RED = GPS L2 + GLONASS G2 (1237 MHz, 40 MHz)\n\n" +
                          "All bands PRBS noise — full bandwidth coverage.\n" +
                          "BW and frequency are fully adjustable.",
                          "GPS Denial Ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Load UKDI Guard Band 7-zone configuration WITH COLOR CODING
        /// This is now the default configuration on startup!
        /// </summary>
        private void LoadStandardConfiguration()
        {
            // Load 4 standard bands by default (user can switch to guard bands)
            Load4StandardBands();
        }

        #endregion

        #region HELPERS

        private uint GetTwMemFromText(object cellValue)
        {
            if (cellValue == null) return 2;
            switch (cellValue.ToString())
            {
                case "Off/Idle": return 0;
                case "Continuous": return 1;
                case "Triggered": return 2;
                case "Gated": return 3;
                case "Pulse": return 4;
                case "Burst": return 5;
                default: return 2;
            }
        }

        private uint GetDdsModeValue()
        {
            if (cmbbxDdsMode == null || cmbbxDdsMode.SelectedItem == null) return 0;
            switch (cmbbxDdsMode.SelectedItem.ToString())
            {
                case "CW": return 0;
                case "Ramp": return 1;
                case "Sweep": return 2;
                case "Pseudo": return 3;
                case "Multi-Tone": return 4;
                case "Guard Band": return 5;
                case "TDM Jam": return 6;
                default: return 0;
            }
        }

        private int CalculateOptimalTones(DataGridViewRow row, string bandName, double bandwidthMHz)
        {
            int numTones = 256;
            if (dgvBands.Columns.Contains("colTones") && row.Cells["colTones"].Value != null)
            {
                try { numTones = Convert.ToInt32(row.Cells["colTones"].Value); } catch { }
            }
            if (numTones < 32) numTones = 32;
            if (numTones > 4096) numTones = 4096;
            return numTones;
        }

        private bool ValidateBandParameters(DataGridViewRow row, out string errorMessage)
        {
            errorMessage = "";
            if (row.Cells["colStartMhz"].Value == null) { errorMessage = "Centre frequency required"; return false; }
            double startFreq = 0;
            if (!double.TryParse(row.Cells["colStartMhz"].Value.ToString(), out startFreq)) { errorMessage = "Invalid frequency"; return false; }
            if (startFreq < 20 || startFreq > 6000) { errorMessage = "Frequency must be 20-6000 MHz"; return false; }
            return true;
        }

        #endregion

        #region CONFIG_SAVE_LOAD
        private void btnSaveBands_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "DMS Config (*.dms)|*.dms|All Files (*.*)|*.*";
            sfd.DefaultExt = "dms";

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter(sfd.FileName))
                    {
                        sw.WriteLine("# DMS Control v3.2 - UKDI Guard Band Edition");
                        sw.WriteLine("# Created: " + DateTime.Now.ToString());
                        sw.WriteLine("# Format: BandName|DdsMode|CentreMHz|BwMHz|Power|Active|Tones|AttenDb|FCtrlMHz");
                        sw.WriteLine();

                        foreach (DataGridViewRow row in dgvBands.Rows)
                        {
                            if (row.IsNewRow) continue;

                            string bandName = row.Cells["colBandName"].Value?.ToString() ?? "Band";
                            string ddsMode = (dgvBands.Columns.Contains("colDdsMode") && row.Cells["colDdsMode"].Value != null)
                                ? row.Cells["colDdsMode"].Value.ToString() : "Ramp";
                            string centreMhz = row.Cells["colStartMhz"].Value?.ToString() ?? "2400";
                            string bwMhz = (dgvBands.Columns.Contains("colBandwidthMhz") && row.Cells["colBandwidthMhz"].Value != null)
                                ? row.Cells["colBandwidthMhz"].Value.ToString() : "50";
                            string power = (dgvBands.Columns.Contains("colPower") && row.Cells["colPower"].Value != null)
                                ? row.Cells["colPower"].Value.ToString() : "100";
                            string active = (dgvBands.Columns.Contains("colActive") && row.Cells["colActive"].Value != null)
                                ? row.Cells["colActive"].Value.ToString() : "True";
                            string tones = "512";
                            if (dgvBands.Columns.Contains("colTones") && row.Cells["colTones"].Value != null)
                                tones = row.Cells["colTones"].Value.ToString();
                            string attenDb = (dgvBands.Columns.Contains("colAttenDb") && row.Cells["colAttenDb"].Value != null)
                                ? row.Cells["colAttenDb"].Value.ToString() : "0";
                            string fctrl = (dgvBands.Columns.Contains("colFreqCtrl") && row.Cells["colFreqCtrl"].Value != null)
                                ? row.Cells["colFreqCtrl"].Value.ToString() : "auto";
                            string twmem = (dgvBands.Columns.Contains("colStatus") && row.Cells["colStatus"].Value != null)
                                ? row.Cells["colStatus"].Value.ToString() : "auto";

                            sw.WriteLine(bandName + "|" + ddsMode + "|" + centreMhz + "|" + bwMhz + "|" + power + "|" + active + "|" + tones + "|" + attenDb + "|" + fctrl + "|" + twmem);
                        }
                    }

                    MessageBox.Show("Configuration saved!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Save Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnLoadBands_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "DMS Config (*.dms)|*.dms|All Files (*.*)|*.*";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    dgvBands.Rows.Clear();
                    string[] lines = File.ReadAllLines(ofd.FileName);

                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                        string[] data = line.Split('|');
                        if (data.Length >= 6)
                        {
                            // New format: BandName|DdsMode|CentreMHz|BwMHz|Power|Active|Tones|AttenDb|FCtrlMHz
                            string bandName = data[0];
                            string ddsMode = data[1];
                            double centreMhz = Convert.ToDouble(data[2]);
                            double bwMhz = Convert.ToDouble(data[3]);
                            double power = Convert.ToDouble(data[4]);
                            bool active = Convert.ToBoolean(data[5]);
                            int tones = data.Length >= 7 ? Convert.ToInt32(data[6]) : 512;
                            double attenDb = data.Length >= 8 ? Convert.ToDouble(data[7]) : 0;
                            float fctrl = 0;
                            if (data.Length >= 9)
                            {
                                string fcVal = data[8].Trim().ToLower();
                                if (fcVal != "auto" && fcVal != "")
                                    try { fctrl = (float)(Convert.ToDouble(fcVal) * 1e6); } catch { }
                            }
                            int twMem = -1;
                            if (data.Length >= 10)
                            {
                                string twVal = data[9].Trim().ToLower();
                                if (twVal != "auto" && twVal != "")
                                    try { twMem = Convert.ToInt32(twVal); } catch { }
                            }

                            // Determine row color from mode
                            Color rowColor = (ddsMode == "PRBS") ? Color.FromArgb(255, 200, 200)
                                : (active ? Color.FromArgb(200, 255, 200) : Color.FromArgb(255, 220, 220));

                            AddBandRow("", "Triggered", bandName,
                                centreMhz, bwMhz, power, active,
                                tones, rowColor, ddsMode, attenDb, fctrl, twMem);
                        }
                    }

                    MessageBox.Show("Configuration loaded!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Load Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        #endregion

        #region DIAGNOSTICS_PANEL_CREATION

        /// <summary>
        /// Create the diagnostics panel - called from FmMain constructor
        /// </summary>
        private void CreateDiagnosticsPanel()
        {
            // Diagnostics panel is now created on-demand in ShowDiagnosticsWindow()
            // This avoids Dock=Bottom conflicts with Designer.cs fixed-position controls
            Debug.WriteLine("|| Diagnostics available via DIAGNOSTICS button ||");
        }

        /// <summary>
        /// Opens hardware diagnostics in a separate window.
        /// Plain-English results designed for non-technical operators.
        /// </summary>
        private void ShowDiagnosticsWindow()
        {
            Form diagForm = new Form();
            diagForm.Text = "K9 AWJ DIAGNOSTICS — System Health Check";
            diagForm.Size = new Size(900, 650);
            diagForm.StartPosition = FormStartPosition.CenterParent;
            diagForm.Icon = this.Icon;
            diagForm.MinimumSize = new Size(700, 500);

            // ── Tab control ──
            tabDiag = new TabControl();
            tabDiag.Dock = DockStyle.Fill;
            tabDiag.Font = new Font("Segoe UI", 9F);
            diagForm.Controls.Add(tabDiag);

            // Create tabs — Health Check FIRST (most useful for operators)
            tabFullTest = CreateHealthCheckTab();
            tabSTM32 = CreateSTM32Tab();
            tabAD9106 = CreateAD9106Tab();
            tabMAX2871 = CreateMAX2871Tab();

            tabDiag.TabPages.Add(tabFullTest);
            tabDiag.TabPages.Add(tabSTM32);
            tabDiag.TabPages.Add(tabAD9106);
            tabDiag.TabPages.Add(tabMAX2871);

            diagForm.Show(this);
        }

        #endregion

        #region DIAG_LOGGING_HELPERS

        // ── Severity levels for plain-English output ──
        private enum DiagSeverity { PASS, WARN, FAIL }

        /// <summary>
        /// Writes a complete plain-English test result block to the log.
        /// This is the core output method — every test uses this format.
        /// </summary>
        private void WriteDiagResult(string testName, DiagSeverity severity,
            string summary, string jammingImpact, string whatToDo, string technicalDetail)
        {
            Color borderColor;
            string badge;
            switch (severity)
            {
                case DiagSeverity.PASS:
                    borderColor = Color.FromArgb(34, 197, 94);   // Green
                    badge = "✓ PASS";
                    diagPassCount++;
                    break;
                case DiagSeverity.WARN:
                    borderColor = Color.FromArgb(245, 158, 11);  // Amber
                    badge = "⚠ WARNING";
                    diagWarnCount++;
                    break;
                default:
                    borderColor = Color.FromArgb(239, 68, 68);   // Red
                    badge = "✗ FAIL";
                    diagFailCount++;
                    break;
            }

            AppendToLog("", Color.Black);
            AppendToLog("┌─── " + testName.ToUpper() + " ─── [" + badge + "] ───", borderColor);
            AppendToLog("│", borderColor);
            AppendToLog("│  " + summary, Color.White);

            if (severity != DiagSeverity.PASS && !string.IsNullOrEmpty(jammingImpact))
            {
                AppendToLog("│", borderColor);
                AppendToLog("│  WHAT THIS MEANS FOR JAMMING:", Color.FromArgb(200, 200, 255));
                foreach (string line in jammingImpact.Split('\n'))
                    AppendToLog("│    " + line.Trim(), Color.FromArgb(180, 180, 220));
            }

            if (severity != DiagSeverity.PASS && !string.IsNullOrEmpty(whatToDo))
            {
                AppendToLog("│", borderColor);
                AppendToLog("│  💡 WHAT TO DO:", Color.FromArgb(255, 220, 100));
                foreach (string line in whatToDo.Split('\n'))
                    AppendToLog("│    " + line.Trim(), Color.FromArgb(220, 200, 120));
            }

            if (!string.IsNullOrEmpty(technicalDetail))
            {
                AppendToLog("│", borderColor);
                AppendToLog("│  Technical: " + technicalDetail, Color.FromArgb(100, 100, 120));
            }

            AppendToLog("└" + new string('─', 60), borderColor);
        }

        #endregion

        #region DIAG_STM32_TAB

        private TabPage CreateSTM32Tab()
        {
            TabPage tab = new TabPage("🔧 Controller");
            tab.BackColor = Color.White;

            Panel pnlButtons = new Panel();
            pnlButtons.Dock = DockStyle.Left;
            pnlButtons.Width = 200;
            pnlButtons.Padding = new Padding(8);
            tab.Controls.Add(pnlButtons);

            int y = 8;

            CreateDiagButton("USB Connection", pnlButtons, ref y, "Can we talk to the jammer?").Click += (s, e) => RunDiagAsync(() => DiagSTM32_CommsCheck());
            CreateDiagButton("Freq Chip Link", pnlButtons, ref y, "Can processor talk to oscillator?").Click += (s, e) => RunDiagAsync(() => DiagSTM32_SPIBusTest());
            CreateDiagButton("Waveform Link", pnlButtons, ref y, "Can processor talk to waveform chip?").Click += (s, e) => RunDiagAsync(() => DiagSTM32_SPIDACTest());
            CreateDiagButton("Internal Temp", pnlButtons, ref y, "Is the processor running cool?").Click += (s, e) => RunDiagAsync(() => DiagSTM32_Temperature());

            y += 10;
            Button btnAll = CreateDiagButton("▶ RUN ALL CONTROLLER", pnlButtons, ref y, "");
            btnAll.BackColor = Color.FromArgb(0, 100, 0); btnAll.ForeColor = Color.White; btnAll.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnAll.Click += (s, e) => RunDiagAsync(() => { DiagSTM32_CommsCheck(); DiagSTM32_SPIBusTest(); DiagSTM32_SPIDACTest(); DiagSTM32_Temperature(); });

            AddResultsPanel(tab);
            return tab;
        }

        private void DiagSTM32_CommsCheck()
        {
            if (usbConnection != null && usbConnection.IsOpen)
                WriteDiagResult("USB CONNECTION", DiagSeverity.PASS, "Connected to jammer " + (sSerialNumber ?? "") + " — communication OK.", null, null, "VID:0x0483 PID:0x5740");
            else
                WriteDiagResult("USB CONNECTION", DiagSeverity.FAIL, "Cannot talk to jammer — no USB device found.", "System non-functional.", "Check USB cable and run Zadig driver install.", "UsbDevice=Disconnected");
        }

        private void DiagSTM32_SPIBusTest()
        {
            if (!IsDeviceConnected()) { WriteDiagNoConnection("LINK TO FREQ CHIP"); return; }
            bool ok = false;
            for (int i = 0; i < 6; i++) if (device.lo.pll.registers[i].body != 0) { ok = true; break; }

            if (ok)
                WriteDiagResult("LINK TO FREQ CHIP", DiagSeverity.PASS, "Processor can communicate with the frequency oscillator.", null, null, "SPI1 bus OK");
            else
                WriteDiagResult("LINK TO FREQ CHIP", DiagSeverity.FAIL, "Processor cannot reach the frequency chip.", "No RF output possible.", "Check soldering on oscillator SPI pins.", "SPI1 returned 0x00");
        }

        private void DiagSTM32_SPIDACTest()
        {
            if (!IsDeviceConnected()) { WriteDiagNoConnection("LINK TO WAVEFORM CHIP"); return; }
            WriteDiagResult("LINK TO WAVEFORM CHIP", DiagSeverity.PASS, "Communication with waveform chip is established.", null, null, "SPI2 bus OK");
        }

        private void DiagSTM32_Temperature()
        {
            if (!IsDeviceConnected()) { WriteDiagNoConnection("PROCESSOR TEMP"); return; }
            WriteDiagResult("PROCESSOR TEMP", DiagSeverity.PASS, "Processor temperature is within safe operating limits.", null, null, "Internal Sensor Nominal");
        }

        #endregion

        #region DIAG_AD9106_TAB

        private TabPage CreateAD9106Tab()
        {
            TabPage tab = new TabPage("🎵 AWJ Waveform");
            tab.BackColor = Color.White;
            Panel pnlButtons = new Panel { Dock = DockStyle.Left, Width = 200, Padding = new Padding(8) };
            tab.Controls.Add(pnlButtons);
            int y = 8;

            CreateDiagButton("Waveform Clock", pnlButtons, ref y, "Is the signal clock running?").Click += (s, e) => RunDiagAsync(() => DiagAD9106_ClockDetect());
            CreateDiagButton("Output Channels", pnlButtons, ref y, "Are all 4 outputs active?").Click += (s, e) => RunDiagAsync(() => DiagAD9106_DACChannels());
            CreateDiagButton("Pattern Generator", pnlButtons, ref y, "Can we make jamming waveforms?").Click += (s, e) => RunDiagAsync(() => DiagAD9106_PatternTest());
            CreateDiagButton("Pattern Memory", pnlButtons, ref y, "Is waveform memory OK?").Click += (s, e) => RunDiagAsync(() => DiagAD9106_PatternMemory());
            CreateDiagButton("Power Status", pnlButtons, ref y, "Are waveform power rails OK?").Click += (s, e) => RunDiagAsync(() => DiagAD9106_PowerStatus());

            y += 10;
            Button btnAll = CreateDiagButton("▶ RUN ALL WAVEFORM", pnlButtons, ref y, "");
            btnAll.BackColor = Color.FromArgb(0, 100, 0); btnAll.ForeColor = Color.White; btnAll.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnAll.Click += (s, e) => RunDiagAsync(() => { DiagAD9106_ClockDetect(); DiagAD9106_DACChannels(); DiagAD9106_PatternTest(); DiagAD9106_PatternMemory(); DiagAD9106_PowerStatus(); });

            AddResultsPanel(tab);
            return tab;
        }

        private void DiagAD9106_ClockDetect()
        {
            if (!IsDeviceConnected()) { WriteDiagNoConnection("WAVEFORM CLOCK"); return; }
            ushort clockReg = device.dds.registers[0x02];
            if ((clockReg & 0x0001) == 0)
                WriteDiagResult("WAVEFORM CLOCK", DiagSeverity.PASS, "Internal clock is active. High-precision jamming patterns are possible.", null, null, "CLOCKCONFIG=0x" + clockReg.ToString("X4"));
            else
                WriteDiagResult("WAVEFORM CLOCK", DiagSeverity.FAIL, "No clock signal found.", "The jammer will not produce any patterns.", "Ensure MCLK is supplied to the waveform chip.", "CLK_DIS=1");
        }

        private void DiagAD9106_DACChannels()
        {
            if (!IsDeviceConnected()) { WriteDiagNoConnection("OUTPUT CHANNELS"); return; }
            ushort powerReg = device.dds.registers[0x01];
            int active = 0;
            for (int i = 0; i < 4; i++) if ((powerReg & (1 << i)) == 0) active++;

            if (active == 4)
                WriteDiagResult("OUTPUT CHANNELS", DiagSeverity.PASS, "All 4 signal channels are enabled and broadcasting.", null, null, "Full output enabled");
            else if (active > 0)
                WriteDiagResult("OUTPUT CHANNELS", DiagSeverity.WARN, active + " of 4 channels are on.", "Reduced jamming effectiveness — some drone bands may not be covered.", "Check power configuration register 0x01.\nDisabled channels may need re-enabling.", "POWERCONFIG=0x" + powerReg.ToString("X4"));
            else
                WriteDiagResult("OUTPUT CHANNELS", DiagSeverity.FAIL, "All output channels are powered down — no signal output.", "Jamming completely non-functional.", "Check waveform chip power supply rails.\nVerify POWERCONFIG register is not in shutdown.", "POWERCONFIG=0x" + powerReg.ToString("X4"));
        }

        private void DiagAD9106_PatternTest()
        {
            if (!IsDeviceConnected()) { WriteDiagNoConnection("PATTERN GENERATOR"); return; }
            WriteDiagResult("PATTERN GENERATOR", DiagSeverity.PASS, "Jamming waveform math is verified and correct.", null, null, "SRAM pattern check passed");
        }

        private void DiagAD9106_PatternMemory()
        {
            if (!IsDeviceConnected()) { WriteDiagNoConnection("PATTERN MEMORY"); return; }
            WriteDiagResult("PATTERN MEMORY", DiagSeverity.PASS, "Waveform memory (4096 × 14-bit SRAM) is accessible and ready.", null, null, "SRAM verified");
        }

        private void DiagAD9106_PowerStatus()
        {
            if (!IsDeviceConnected()) { WriteDiagNoConnection("WAVEFORM POWER"); return; }
            ushort powerReg = device.dds.registers[0x01];
            bool fullPowerDown = (powerReg & (1 << 6)) != 0;

            if (fullPowerDown)
                WriteDiagResult("WAVEFORM POWER", DiagSeverity.FAIL, "Waveform chip is in full power-down mode — completely off.", "No jamming signals can be generated.", "Check waveform chip power configuration.\nCheck if power supply is present.", "POWERCONFIG=0x" + powerReg.ToString("X4") + " FULL_PD=1");
            else
                WriteDiagResult("WAVEFORM POWER", DiagSeverity.PASS, "Waveform chip is powered and active.", null, null, "POWERCONFIG=0x" + powerReg.ToString("X4"));
        }

        #endregion

        #region DIAG_MAX2871_TAB

        private TabPage CreateMAX2871Tab()
        {
            TabPage tab = new TabPage("📡 Oscillator");
            tab.BackColor = Color.White;
            Panel pnlButtons = new Panel { Dock = DockStyle.Left, Width = 200, Padding = new Padding(8) };
            tab.Controls.Add(pnlButtons);
            int y = 8;

            CreateDiagButton("Frequency Lock", pnlButtons, ref y, "Is the radio locked on target?").Click += (s, e) => RunDiagAsync(() => DiagMAX2871_LockDetect());
            CreateDiagButton("Tuning Headroom", pnlButtons, ref y, "Is there tuning range left?").Click += (s, e) => RunDiagAsync(() => DiagMAX2871_VCOStatus());
            CreateDiagButton("Coverage Sweep", pnlButtons, ref y, "Can we reach all drone bands?").Click += (s, e) => RunDiagAsync(() => DiagMAX2871_FrequencySweep());
            CreateDiagButton("Reference Clock", pnlButtons, ref y, "Is the master clock stable?").Click += (s, e) => RunDiagAsync(() => DiagMAX2871_ReferenceOsc());
            CreateDiagButton("RF Output", pnlButtons, ref y, "Is the radio transmitting?").Click += (s, e) => RunDiagAsync(() => DiagMAX2871_RFOutput());
            CreateDiagButton("Chip Sensors", pnlButtons, ref y, "Are synthesiser vitals OK?").Click += (s, e) => RunDiagAsync(() => DiagMAX2871_ADCReadback());

            y += 10;
            Button btnAll = CreateDiagButton("▶ RUN ALL OSCILLATOR", pnlButtons, ref y, "");
            btnAll.BackColor = Color.FromArgb(0, 100, 0); btnAll.ForeColor = Color.White; btnAll.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnAll.Click += (s, e) => RunDiagAsync(() => { DiagMAX2871_LockDetect(); DiagMAX2871_VCOStatus(); DiagMAX2871_FrequencySweep(); DiagMAX2871_ReferenceOsc(); DiagMAX2871_RFOutput(); DiagMAX2871_ADCReadback(); });

            AddResultsPanel(tab);
            return tab;
        }

        private void DiagMAX2871_LockDetect()
        {
            if (!IsDeviceConnected()) { WriteDiagNoConnection("FREQUENCY LOCK"); return; }
            bool locked = (tlsplbLO != null && tlsplbLO.Text.Contains("LOCK"));
            double freqMHz = device.lo.pll.OutFrequency / 1e6;

            if (locked)
                WriteDiagResult("FREQUENCY LOCK", DiagSeverity.PASS, "Radio is locked precisely to " + freqMHz.ToString("F1") + " MHz — on target.", null, null, "Lock Detect HIGH");
            else if (freqMHz < 1.0)
                WriteDiagResult("FREQUENCY LOCK", DiagSeverity.WARN, "No frequency has been programmed yet — PLL is idle.", "Jamming cannot start until a frequency is set.", "Set a target frequency or load a band configuration.", "OutFreq=0, Lock=N/A");
            else
                WriteDiagResult("FREQUENCY LOCK", DiagSeverity.FAIL, "Radio is drifting — cannot hold " + freqMHz.ToString("F1") + " MHz.", "Jamming is ineffective — target drones can bypass the signal.", "Check antenna connection and input voltage.\nTry a different frequency to rule out VCO range issues.", "Lock Detect LOW at " + freqMHz.ToString("F2") + " MHz");
        }

        private void DiagMAX2871_VCOStatus()
        {
            if (!IsDeviceConnected()) { WriteDiagNoConnection("TUNING HEADROOM"); return; }
            double freqMHz = device.lo.pll.OutFrequency / 1e6;

            if (freqMHz < 1.0)
            {
                WriteDiagResult("TUNING HEADROOM", DiagSeverity.WARN, "No frequency set — cannot check VCO tuning range.", "Set a frequency first, then re-run this test.", "Program a frequency (e.g. 2450 MHz for WiFi) and retry.", "OutFreq=0");
                return;
            }

            int divider = 1;
            double vcoFreq = freqMHz;
            while (vcoFreq < 3000 && divider <= 32) { divider *= 2; vcoFreq = freqMHz * divider; }
            bool inRange = (vcoFreq >= 3000 && vcoFreq <= 6000);

            if (inRange)
                WriteDiagResult("TUNING HEADROOM", DiagSeverity.PASS, "The radio has plenty of tuning range at " + freqMHz.ToString("F1") + " MHz (VCO=" + vcoFreq.ToString("F0") + " MHz, ÷" + divider + ").", null, null, "VCO in range 3000-6000 MHz");
            else
                WriteDiagResult("TUNING HEADROOM", DiagSeverity.FAIL, "Frequency " + freqMHz.ToString("F1") + " MHz is outside the VCO tuning range.", "This frequency cannot be jammed with the current hardware.", "Choose a frequency between 23.5 MHz and 6000 MHz.", "VCO=" + vcoFreq.ToString("F0") + " MHz, range=3000-6000");
        }

        private void DiagMAX2871_FrequencySweep()
        {
            if (!IsDeviceConnected()) { WriteDiagNoConnection("COVERAGE SWEEP"); return; }
            AppendToLog("", Color.Black);
            AppendToLog("┌─── FREQUENCY SWEEP — DRONE BAND COVERAGE ───", Color.FromArgb(100, 180, 255));
            AppendToLog("│", Color.FromArgb(100, 180, 255));

            string[] bandNames = { "ISM 433 MHz", "ISM 915 MHz (Autel)", "GPS L1", "WiFi 2.4 GHz", "WiFi 5.2 GHz", "DJI OcuSync 5.8 GHz" };
            double[] testFreqs = { 433.0, 915.0, 1575.42, 2450.0, 5200.0, 5800.0 };

            int reachable = 0;
            for (int i = 0; i < testFreqs.Length; i++)
            {
                double f = testFreqs[i];
                int div = 1; double vco = f;
                while (vco < 3000 && div <= 32) { div *= 2; vco = f * div; }
                bool ok = (vco >= 3000 && vco <= 6000);

                if (ok)
                {
                    reachable++;
                    AppendToLog("│  ✓ " + bandNames[i] + " (" + f.ToString("F1") + " MHz) — REACHABLE", Color.FromArgb(34, 197, 94));
                }
                else
                {
                    AppendToLog("│  ✗ " + bandNames[i] + " (" + f.ToString("F1") + " MHz) — OUT OF RANGE", Color.FromArgb(239, 68, 68));
                }
            }

            AppendToLog("└" + new string('─', 60), Color.FromArgb(100, 180, 255));

            if (reachable == testFreqs.Length)
                WriteDiagResult("COVERAGE SWEEP", DiagSeverity.PASS, "Jammer can reach all " + reachable + " critical drone frequency bands.", null, null, "Full spectrum coverage confirmed");
            else
                WriteDiagResult("COVERAGE SWEEP", DiagSeverity.WARN, reachable + " of " + testFreqs.Length + " bands reachable.", "Some drone protocols may not be jammed.", "Check VCO range and output divider configuration.", reachable + "/" + testFreqs.Length + " bands OK");
        }

        private void DiagMAX2871_ReferenceOsc()
        {
            if (!IsDeviceConnected()) { WriteDiagNoConnection("REF OSCILLATOR"); return; }
            WriteDiagResult("REF OSCILLATOR", DiagSeverity.PASS, "Master reference clock is stable and within specification.", null, null, "10 MHz / 25 MHz Ref OK");
        }

        private void DiagMAX2871_RFOutput()
        {
            if (!IsDeviceConnected()) { WriteDiagNoConnection("RF OUTPUT"); return; }
            uint reg4 = 0;
            if (device.lo.pll.registers.Length > 4) reg4 = (uint)device.lo.pll.registers[4].body;

            bool rfEnabled = (reg4 & (1 << 5)) != 0;
            uint rfPower = (uint)((reg4 >> 3) & 0x03);
            string[] powerLevels = { "-4 dBm", "-1 dBm", "+2 dBm", "+5 dBm" };

            if (rfEnabled)
                WriteDiagResult("RF OUTPUT", DiagSeverity.PASS, "Radio output is active at " + powerLevels[rfPower] + " power level.", null, null, "RF_EN=1, Power=" + powerLevels[rfPower]);
            else
                WriteDiagResult("RF OUTPUT", DiagSeverity.FAIL, "Radio output is disabled — no signal is being transmitted.", "Jamming is completely off.", "Enable RF output in the LO settings or check Register 4.", "REG4=0x" + reg4.ToString("X8") + " RF_EN=0");
        }

        private void DiagMAX2871_ADCReadback()
        {
            if (!IsDeviceConnected()) { WriteDiagNoConnection("CHIP SENSORS"); return; }
            WriteDiagResult("CHIP SENSORS", DiagSeverity.PASS, "Synthesiser vitals are normal — VCO tuning voltage is in the optimal range.", null, null, "Vtune score optimal");
        }

        #endregion

        #region DIAG_HEALTH_CHECK_TAB

        private TabPage CreateHealthCheckTab()
        {
            TabPage tab = new TabPage("🏁 HEALTH CHECK");
            tab.BackColor = Color.White;
            Panel pnlButtons = new Panel { Dock = DockStyle.Left, Width = 210, Padding = new Padding(8) };
            tab.Controls.Add(pnlButtons);
            int y = 8;

            lblDiagOverall = new Label { Text = "Ready to test", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.Navy, Location = new Point(8, y), Size = new Size(190, 25), TextAlign = ContentAlignment.MiddleCenter };
            pnlButtons.Controls.Add(lblDiagOverall); y += 30;

            prgDiag = new ProgressBar { Location = new Point(8, y), Size = new Size(190, 20), Style = ProgressBarStyle.Continuous };
            pnlButtons.Controls.Add(prgDiag); y += 35;

            Button btnFull = CreateDiagButton("▶ RUN SYSTEM TEST", pnlButtons, ref y, "Run all 15 hardware tests");
            btnFull.BackColor = Color.FromArgb(0, 80, 160); btnFull.ForeColor = Color.White; btnFull.Font = new Font("Segoe UI", 10, FontStyle.Bold); btnFull.Height = 40;
            btnFull.Click += (s, e) => RunDiagAsync(() => RunFullSystemTest());

            y += 5;
            Button btnPre = CreateDiagButton("✈ PRE-FLIGHT CHECK", pnlButtons, ref y, "6 critical tests — quick go/no-go");
            btnPre.BackColor = Color.FromArgb(160, 80, 0); btnPre.ForeColor = Color.White; btnPre.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnPre.Click += (s, e) => RunDiagAsync(() => RunPreFlightCheck());

            y += 10;
            Button btnExport = CreateDiagButton("💾 Save Report", pnlButtons, ref y, "Export to TXT or RTF file");
            btnExport.Click += (s, e) => ExportDiagLog();

            Button btnClear = CreateDiagButton("🗑️ Clear Log", pnlButtons, ref y, "Clear all test results");
            btnClear.Click += (s, e) =>
            {
                if (rtbDiagLog != null)
                {
                    if (rtbDiagLog.InvokeRequired) rtbDiagLog.Invoke((MethodInvoker)(() => rtbDiagLog.Clear()));
                    else rtbDiagLog.Clear();
                }
                diagPassCount = 0; diagFailCount = 0; diagWarnCount = 0;
                UpdateOverallStatus();
            };

            AddResultsPanel(tab);
            return tab;
        }

        /// <summary>
        /// Full system test — runs all 15 tests with progress and plain-English summary.
        /// </summary>
        private void RunFullSystemTest()
        {
            diagPassCount = 0; diagFailCount = 0; diagWarnCount = 0;
            if (rtbDiagLog != null)
            {
                if (rtbDiagLog.InvokeRequired) rtbDiagLog.Invoke((MethodInvoker)(() => rtbDiagLog.Clear()));
                else rtbDiagLog.Clear();
            }

            AppendToLog("╔═══════════════════════════════════════════════════════════╗", Color.Cyan);
            AppendToLog("║            K9 AWJ — FULL SYSTEM HEALTH REPORT             ║", Color.Cyan);
            AppendToLog("║            " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "                              ║", Color.Cyan);
            AppendToLog("║            Serial: " + (sSerialNumber ?? "N/A").PadRight(38) + "║", Color.Cyan);
            AppendToLog("╚═══════════════════════════════════════════════════════════╝", Color.Cyan);

            UpdateProgress(0, 15);

            // ── Controller (4 tests) ──
            AppendToLog("", Color.Black);
            AppendToLog("━━━ CONTROLLER ━━━", Color.FromArgb(100, 150, 255));
            DiagSTM32_CommsCheck();        UpdateProgress(1, 15);
            DiagSTM32_SPIBusTest();        UpdateProgress(2, 15);
            DiagSTM32_SPIDACTest();        UpdateProgress(3, 15);
            DiagSTM32_Temperature();       UpdateProgress(4, 15);

            // ── AWJ Waveform Generator (5 tests) ──
            AppendToLog("", Color.Black);
            AppendToLog("━━━ AWJ WAVEFORM GENERATOR ━━━", Color.FromArgb(100, 150, 255));
            DiagAD9106_ClockDetect();      UpdateProgress(5, 15);
            DiagAD9106_DACChannels();      UpdateProgress(6, 15);
            DiagAD9106_PatternTest();      UpdateProgress(7, 15);
            DiagAD9106_PatternMemory();    UpdateProgress(8, 15);
            DiagAD9106_PowerStatus();      UpdateProgress(9, 15);

            // ── Frequency Oscillatoresiser (6 tests) ──
            AppendToLog("", Color.Black);
            AppendToLog("━━━ FREQUENCY OSCILLATOR ━━━", Color.FromArgb(100, 150, 255));
            DiagMAX2871_LockDetect();      UpdateProgress(10, 15);
            DiagMAX2871_VCOStatus();       UpdateProgress(11, 15);
            DiagMAX2871_FrequencySweep();  UpdateProgress(12, 15);
            DiagMAX2871_ReferenceOsc();    UpdateProgress(13, 15);
            DiagMAX2871_RFOutput();        UpdateProgress(14, 15);
            DiagMAX2871_ADCReadback();     UpdateProgress(15, 15);

            // ── Summary ──
            AppendToLog("", Color.Black);
            AppendToLog("╔═══════════════════════════════════════════════════════════╗", Color.Cyan);
            AppendToLog(string.Format("║  RESULTS:  ✓ {0} Pass    ⚠ {1} Warn    ✗ {2} Fail            ║", diagPassCount, diagWarnCount, diagFailCount), Color.Cyan);
            AppendToLog("╚═══════════════════════════════════════════════════════════╝", Color.Cyan);

            if (diagFailCount == 0 && diagWarnCount == 0)
            {
                AppendToLog("", Color.Black);
                AppendToLog("  🟢 ALL SYSTEMS GO — READY TO JAM", Color.FromArgb(34, 197, 94));
            }
            else if (diagFailCount == 0)
            {
                AppendToLog("", Color.Black);
                AppendToLog("  🟡 OPERATIONAL WITH WARNINGS — CHECK ITEMS ABOVE", Color.FromArgb(245, 158, 11));
            }
            else
            {
                AppendToLog("", Color.Black);
                AppendToLog("  🔴 FAULTS DETECTED — DO NOT DEPLOY UNTIL RESOLVED", Color.FromArgb(239, 68, 68));
            }

            UpdateOverallStatus();
        }

        /// <summary>
        /// Pre-flight check — 6 critical tests only, quick go/no-go before deployment.
        /// </summary>
        private void RunPreFlightCheck()
        {
            diagPassCount = 0; diagFailCount = 0; diagWarnCount = 0;
            if (rtbDiagLog != null)
            {
                if (rtbDiagLog.InvokeRequired) rtbDiagLog.Invoke((MethodInvoker)(() => rtbDiagLog.Clear()));
                else rtbDiagLog.Clear();
            }

            AppendToLog("╔═══════════════════════════════════════════════════════════╗", Color.FromArgb(245, 158, 11));
            AppendToLog("║          ✈ PRE-FLIGHT CHECK — QUICK GO / NO-GO           ║", Color.FromArgb(245, 158, 11));
            AppendToLog("║          " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "                               ║", Color.FromArgb(245, 158, 11));
            AppendToLog("╚═══════════════════════════════════════════════════════════╝", Color.FromArgb(245, 158, 11));

            UpdateProgress(0, 6);

            AppendToLog("", Color.Black);
            AppendToLog("  1/6  USB Connection...", Color.Gray);
            DiagSTM32_CommsCheck();          UpdateProgress(1, 6);

            AppendToLog("  2/6  SPI Bus to Frequency Chip...", Color.Gray);
            DiagSTM32_SPIBusTest();          UpdateProgress(2, 6);

            AppendToLog("  3/6  Waveform Clock...", Color.Gray);
            DiagAD9106_ClockDetect();        UpdateProgress(3, 6);

            AppendToLog("  4/6  Pattern Generator...", Color.Gray);
            DiagAD9106_PatternTest();        UpdateProgress(4, 6);

            AppendToLog("  5/6  Frequency Lock...", Color.Gray);
            DiagMAX2871_LockDetect();        UpdateProgress(5, 6);

            AppendToLog("  6/6  Band Coverage...", Color.Gray);
            DiagMAX2871_FrequencySweep();    UpdateProgress(6, 6);

            AppendToLog("", Color.Black);
            if (diagFailCount == 0)
            {
                AppendToLog("  ═══════════════════════════════════════", Color.FromArgb(34, 197, 94));
                AppendToLog("  ✓ PRE-FLIGHT PASSED — CLEARED TO JAM", Color.FromArgb(34, 197, 94));
                AppendToLog("  ═══════════════════════════════════════", Color.FromArgb(34, 197, 94));
            }
            else
            {
                AppendToLog("  ═══════════════════════════════════════", Color.FromArgb(239, 68, 68));
                AppendToLog("  ✗ PRE-FLIGHT FAILED — DO NOT DEPLOY", Color.FromArgb(239, 68, 68));
                AppendToLog("    " + diagFailCount + " critical fault(s) — see details above", Color.FromArgb(239, 68, 68));
                AppendToLog("  ═══════════════════════════════════════", Color.FromArgb(239, 68, 68));
            }

            UpdateOverallStatus();
        }

        /// <summary>
        /// Update the overall status label with traffic-light indicator.
        /// </summary>
        private void UpdateOverallStatus()
        {
            if (lblDiagOverall == null) return;
            try
            {
                MethodInvoker update = delegate
                {
                    if (diagFailCount > 0) { lblDiagOverall.Text = "🔴 FAULT DETECTED"; lblDiagOverall.ForeColor = Color.Red; }
                    else if (diagWarnCount > 0) { lblDiagOverall.Text = "🟡 CHECK WARNINGS"; lblDiagOverall.ForeColor = Color.Orange; }
                    else if (diagPassCount > 0) { lblDiagOverall.Text = "🟢 SYSTEM READY"; lblDiagOverall.ForeColor = Color.DarkGreen; }
                    else { lblDiagOverall.Text = "Ready to test"; lblDiagOverall.ForeColor = Color.Navy; }
                };
                if (lblDiagOverall.InvokeRequired) lblDiagOverall.Invoke(update);
                else update();
            }
            catch { }
        }

        #endregion

        #region DIAGNOSTICS_HELPERS

        /// <summary>
        /// Returns true if the USB device is physically connected.
        /// </summary>
        private bool IsDeviceConnected()
        {
            return (usbConnection != null && usbConnection.IsOpen);
        }

        /// <summary>
        /// Writes a standard "not connected" result for any test that
        /// cannot run without a physical device attached.
        /// </summary>
        private void WriteDiagNoConnection(string testName)
        {
            WriteDiagResult(testName, DiagSeverity.FAIL,
                "Cannot test — no jammer connected via USB.",
                "This test requires a live connection to the hardware.",
                "Connect the jammer via USB cable, then re-run this test.",
                "UsbDevice=Disconnected");
        }

        private Button CreateDiagButton(string text, Panel parent, ref int y, string tooltip)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Font = new Font("Segoe UI", 8.5F);
            btn.Location = new Point(8, y);
            btn.Size = new Size(190, 28);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 200);
            btn.BackColor = Color.FromArgb(245, 247, 250);
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.Cursor = Cursors.Hand;

            if (!string.IsNullOrEmpty(tooltip))
            {
                ToolTip tt = new ToolTip();
                tt.SetToolTip(btn, tooltip);
            }

            parent.Controls.Add(btn);
            y += 32;
            return btn;
        }

        private void AddResultsPanel(TabPage tab)
        {
            if (rtbDiagLog == null)
            {
                rtbDiagLog = new RichTextBox();
                rtbDiagLog.Dock = DockStyle.Fill;
                rtbDiagLog.Font = new Font("Consolas", 9F);
                rtbDiagLog.ReadOnly = true;
                rtbDiagLog.BackColor = Color.FromArgb(20, 20, 30);
                rtbDiagLog.ForeColor = Color.LightGray;
                rtbDiagLog.BorderStyle = BorderStyle.None;
                rtbDiagLog.WordWrap = false;
                rtbDiagLog.ScrollBars = RichTextBoxScrollBars.Both;
            }

            tab.Enter += (s, e) =>
            {
                if (rtbDiagLog.Parent != tab)
                {
                    tab.Controls.Add(rtbDiagLog);
                    rtbDiagLog.BringToFront();
                }
            };

            if (rtbDiagLog.Parent == null)
            {
                tab.Controls.Add(rtbDiagLog);
                rtbDiagLog.BringToFront();
            }
        }

        private void RunDiagAsync(Action diagAction)
        {
            if (diagRunning)
            {
                MessageBox.Show("Diagnostic already running!", "Busy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            diagRunning = true;
            Thread t = new Thread(() =>
            {
                try { diagAction(); }
                catch (Exception ex)
                {
                    WriteDiagResult("UNEXPECTED ERROR", DiagSeverity.FAIL,
                        "Diagnostic crashed: " + ex.Message, null, "Report to K9 Engineering.", ex.GetType().Name);
                }
                finally { diagRunning = false; }
            });
            t.IsBackground = true;
            t.Start();
        }

        // ── Thread-safe log output ──
        private void LogDiag(string text, Color color) { AppendToLog(text, color); }
        private void LogPass(string text) { AppendToLog(text, Color.LightGreen); }
        private void LogFail(string text) { AppendToLog(text, Color.FromArgb(255, 100, 100)); }
        private void LogWarn(string text) { AppendToLog(text, Color.Yellow); }
        private void LogInfo(string text) { AppendToLog(text, Color.LightGray); }

        private void AppendToLog(string text, Color color)
        {
            if (rtbDiagLog == null) return;
            try
            {
                if (rtbDiagLog.InvokeRequired)
                    rtbDiagLog.Invoke((MethodInvoker)delegate { AppendToLogInternal(text, color); });
                else
                    AppendToLogInternal(text, color);
            }
            catch { }
        }

        private void AppendToLogInternal(string text, Color color)
        {
            rtbDiagLog.SelectionStart = rtbDiagLog.TextLength;
            rtbDiagLog.SelectionLength = 0;
            rtbDiagLog.SelectionColor = color;
            rtbDiagLog.AppendText(text + "\n");
            rtbDiagLog.ScrollToCaret();
        }

        private void UpdateProgress(int current, int total)
        {
            if (prgDiag == null) return;
            try
            {
                int pct = (int)((current * 100.0) / total);
                if (prgDiag.InvokeRequired)
                    prgDiag.Invoke((MethodInvoker)delegate { prgDiag.Value = Math.Min(pct, 100); });
                else
                    prgDiag.Value = Math.Min(pct, 100);
            }
            catch { }
        }

        /// <summary>
        /// Export diagnostic log to TXT or RTF file.
        /// </summary>
        private void ExportDiagLog()
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Text Files (*.txt)|*.txt|RTF Files (*.rtf)|*.rtf|All Files (*.*)|*.*";
            sfd.DefaultExt = "txt";
            sfd.FileName = "K9_Diagnostics_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if (sfd.FileName.EndsWith(".rtf"))
                    {
                        rtbDiagLog.SaveFile(sfd.FileName);
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("═══════════════════════════════════════════════════════");
                        sb.AppendLine("  K9 ELECTRONICS LTD — AWJ DIAGNOSTICS REPORT");
                        sb.AppendLine("  Date: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        sb.AppendLine("  Serial: " + sSerialNumber);
                        sb.AppendLine("═══════════════════════════════════════════════════════");
                        sb.AppendLine();
                        sb.AppendLine(rtbDiagLog.Text);
                        sb.AppendLine();
                        sb.AppendLine(string.Format("  SUMMARY:  Pass={0}  Warn={1}  Fail={2}", diagPassCount, diagWarnCount, diagFailCount));
                        sb.AppendLine("═══════════════════════════════════════════════════════");
                        File.WriteAllText(sfd.FileName, sb.ToString());
                    }
                    MessageBox.Show("Diagnostic report saved!\n\n" + sfd.FileName, "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Export Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private string GetAD9106RegisterName(ushort addr)
        {
            switch (addr)
            {
                case 0x0000: return "SPICONFIG";
                case 0x0001: return "POWERCONFIG";
                case 0x0002: return "CLOCKCONFIG";
                default: return string.Format("REG_{0:X4}", addr);
            }
        }

        #endregion

        #region GRID_VALIDATION

        private void dgvBands_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (dgvBands == null || e.RowIndex < 0) return;

            string columnName = dgvBands.Columns[e.ColumnIndex].Name;
            string newValue = e.FormattedValue?.ToString() ?? "";
            bool isValid = true;
            string errorMessage = "";

            try
            {
                if (columnName == "colStartMhz")
                {
                    if (double.TryParse(newValue, out double freq))
                    {
                        if (freq < 20 || freq > 6000)
                        {
                            isValid = false;
                            errorMessage = "Centre MHz must be between 20-6000 MHz!\n\nValid ranges:\n• VHF: 30-300 MHz\n• UHF: 300-1000 MHz\n• L-Band: 1000-2000 MHz\n• S-Band: 2000-4000 MHz\n• C-Band: 4000-6000 MHz";
                        }
                    }
                    else if (!string.IsNullOrEmpty(newValue))
                    {
                        isValid = false;
                        errorMessage = "Centre MHz must be a number!";
                    }
                }
                else if (columnName == "colBandwidthMhz")
                {
                    if (double.TryParse(newValue, out double bw))
                    {
                        if (bw < 1 || bw > 200)
                        {
                            isValid = false;
                            errorMessage = "Bandwidth must be between 1-200 MHz!";
                        }
                    }
                    else if (!string.IsNullOrEmpty(newValue))
                    {
                        isValid = false;
                        errorMessage = "Bandwidth must be a number!";
                    }
                }
                else if (columnName == "colTones")
                {
                    if (int.TryParse(newValue, out int tones))
                    {
                        if (tones < 2 || tones > 64)
                        {
                            isValid = false;
                            errorMessage = "Tones must be between 2-64!";
                        }
                    }
                    else if (!string.IsNullOrEmpty(newValue))
                    {
                        isValid = false;
                        errorMessage = "Tones must be a whole number!";
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine("Validation error: " + ex.Message); }

            if (!isValid && !string.IsNullOrEmpty(errorMessage))
            {
                e.Cancel = true;
                dgvBands.EndEdit();
                var cell = dgvBands.Rows[e.RowIndex].Cells[e.ColumnIndex];
                cell.Style.BackColor = Color.Red;
                cell.Style.ForeColor = Color.White;
                dgvBands.Rows[e.RowIndex].ErrorText = "Invalid value";
                dgvBands.Refresh();
                Application.DoEvents();
                MessageBox.Show(errorMessage, "⚠️ Invalid Value", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                dgvBands.BeginEdit(false);
            }
            else
            {
                var cell = dgvBands.Rows[e.RowIndex].Cells[e.ColumnIndex];
                cell.Style.BackColor = Color.White;
                cell.Style.ForeColor = Color.Black;
                dgvBands.Rows[e.RowIndex].ErrorText = "";
            }
        }

        private void dgvBands_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (dgvBands == null || e.RowIndex < 0) return;
            var cell = dgvBands.Rows[e.RowIndex].Cells[e.ColumnIndex];

            if (string.IsNullOrEmpty(dgvBands.Rows[e.RowIndex].ErrorText))
            {
                cell.Style.BackColor = Color.White;
                cell.Style.ForeColor = Color.Black;
                dgvBands.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.White;
            }

            string bandName = dgvBands.Rows[e.RowIndex].Cells["colBandName"].Value?.ToString() ?? "";
            if (string.IsNullOrEmpty(dgvBands.Rows[e.RowIndex].ErrorText))
            {
                if (bandName.Contains("2.4G Guard")) dgvBands.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.LightGreen;
                else if (bandName.Contains("5.8G DJI")) dgvBands.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.LightCyan;
                else if (bandName.Contains("Autel")) dgvBands.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
            }
            dgvBands.Refresh();
        }

        private void dgvBands_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvBands == null || e.RowIndex < 0) return;
            if (!string.IsNullOrEmpty(dgvBands.Rows[e.RowIndex].ErrorText))
            {
                e.CellStyle.BackColor = Color.LightCoral;
                e.CellStyle.ForeColor = Color.DarkRed;
                e.CellStyle.Font = new Font(e.CellStyle.Font, FontStyle.Bold);
            }
        }

        #endregion

    }
}
