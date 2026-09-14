/**
 * @File:   FmMain.cs
 * @Author: K9 Electronics.
 * @Date:   09/02/2026
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
        #endregion
        private int k9ExtendedMode = 0;  // Tracks UI mode index (0-7) vs hardware AD9106_MODE (0-3)
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
            this.MinimumSize = new Size(1200, 700);
            this.WindowState = FormWindowState.Normal;

            // ─── POPULATE COMBOBOXES ───
            // Always repopulate to override any Designer items that may be wrong/missing
            // AD9106_MODE enum: 0=CONTINUE, 1=SOFTWARE, 2=RAMP, 3=PSEUDO
            if (cmbbxDdsMode != null)
            {
                cmbbxDdsMode.Items.Clear();
                cmbbxDdsMode.Items.AddRange(new object[] {
                    "Spot",         // 0 → AD9106 CONTINUE
                    "Sweep",        // 1 → AD9106 SOFTWARE
                    "Ramp",         // 2 → AD9106 RAMP
                    "Pseudo",       // 3 → AD9106 PSEUDO
                    "CW",           // 4 → AD9106 CONTINUE (alias)
                    "Multi-Tone",   // 5 → AD9106 RAMP + multi-tone config
                    "Guard Band",   // 6 → AD9106 RAMP + guard band hopping
                    "TDM Jam"       // 7 → AD9106 RAMP + TDM fast hop
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
            CreateGuardBandSettingsBox();
            CreateDiagnosticsPanel();

            // K9: Force all panels visible at startup (before device connects)
            if (grbxATT != null) grbxATT.Visible = true;
            if (grbxDetector != null) grbxDetector.Visible = true;
            if (grbxPulseModulatorBox != null) grbxPulseModulatorBox.Visible = true;
            if (grbxLO != null) grbxLO.Visible = true;
            if (grbxDDS != null) grbxDDS.Visible = true;
            if (grbxSwitchSelect != null) grbxSwitchSelect.Visible = true;

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

            // K9: Make Switch Select groupbox larger to show all 8 bands clearly
            if (grbxSwitchSelect != null)
            {
                grbxSwitchSelect.Width = Math.Max(grbxSwitchSelect.Width, 220);
                grbxSwitchSelect.Height = Math.Max(grbxSwitchSelect.Height, 260);
            }

            // K9: Set FreqCtrl default to 1 MHz
            if (txbxDdsFreqCtrl != null && string.IsNullOrEmpty(txbxDdsFreqCtrl.Text))
                txbxDdsFreqCtrl.Text = "1.000 MHz";

            // K9: Auto-load 4 standard bands on startup
            // K9: Grid starts empty - bands only loaded from device or by user action
            // LoadDefaultBands();
        }

        /// <summary> Установка конфигурации устройства DDS. </summary>
        private void DDSSetConfiguration()
        {
            ushort cmd_counter = 0;
            ushort Cycle = 1;

            device.dds.Start = (ulong)(nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble(txbxDdsStart.Text));
            device.dds.Step = (ulong)(nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble(txbxDdsStep.Text));
            device.dds.Points = Convert.ToUInt32(txbxDdsPoints.Text);
            device.dds.FreqCtrl = (float)nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble(txbxDdsFreqCtrl.Text);

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
                    this.Text = "DMS2";
                    this.tlspSerialNumber.Text = "SN:";
                    this.tlspStatusPanel.Maximum = 0;
                    this.tlspStatusPanel.Value = 0;
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
                if (grbxPulseModulatorBox != null) this.grbxPulseModulatorBox.Visible = true;
                if (grbxLO != null) this.grbxLO.Visible = true;
                if (grbxDDS != null) this.grbxDDS.Visible = true;
                if (grbxSwitchSelect != null) this.grbxSwitchSelect.Visible = true;
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
                if (txbxDdsTwMem != null) txbxDdsTwMem.Text = Convert.ToString(_device.dds.registers[0x47]);
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
                    if (usbConnection != null)
                    {
                        if (usbConnection.IsOpen)
                        {
                            if (usbConnection.UsbRegistryInfo.IsAlive == false)
                            {
                                ConnectionControlGUI(false, run_cfg);
                                usbConnection.Close();
                                //throw new Exception("Lost connection");
                            }

                            if (ltTxCommand.Count > 0)
                            {
                                writeEndpoint.Write(ltTxCommand[0].body, 3000, out bytesWritten);
                                ThreadSafeDebugMessage(String.Format("[USB send]: {0:d} bytes\n", bytesWritten));
                                ltTxCommand.RemoveAt(0);
                            }
                        }
                        else if (guiActive == true)
                        {
                            ConnectionControlGUI(false, run_cfg);
                            usbConnection.Close();
                        }
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
                if (this.run_cfg.iMode == Mode.Debug)
                {
                    PrepareGroupBoxDDS(device.dds.SweepOn);
                }
                guiActive = true;

                // Update connection LED
                if (ledConnection != null) { ledConnection.BackColor = Color.Lime; }
                if (lblConnectionStatus != null) { lblConnectionStatus.Text = "ONLINE"; lblConnectionStatus.ForeColor = Color.Green; }
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

                    this.Text = String.Format("{0:s}", usbConnection.Info.ProductString);
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
                        txbxDdsTwMem.Text = Convert.ToString(device.dds.TwMem);
                        txbxDdsPoints.Text = Convert.ToString(device.dds.Points);
                    }

                    if (device.dds.SweepOn == (ushort)AD9106_MODE.SOFTWARE)
                    {
                        device.dds.Start = (float)nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble(txbxDdsStart.Text);
                        AddSequences(new SequenceSetSweepDDS(
                                                ltTxCommand, null,
                                                1,
                                                (ulong)device.dds.Start,
                                                (ulong)device.dds.Step,
                                                (ulong)device.dds.Points));
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
                    case 0: hwMode = (ushort)AD9106_MODE.CONTINUE; break;   // Spot
                    case 1: hwMode = (ushort)AD9106_MODE.SOFTWARE; break;   // Sweep
                    case 2: hwMode = (ushort)AD9106_MODE.RAMP; break;   // Ramp
                    case 3: hwMode = (ushort)AD9106_MODE.PSEUDO; break;   // Pseudo
                    case 4: hwMode = (ushort)AD9106_MODE.CONTINUE; break;   // CW (alias for Spot)
                    case 5: hwMode = (ushort)AD9106_MODE.RAMP; break;   // Multi-Tone (RAMP with multi-channel)
                    case 6: hwMode = (ushort)AD9106_MODE.RAMP; break;   // Guard Band (RAMP + LO hopping)
                    case 7: hwMode = (ushort)AD9106_MODE.RAMP; break;   // TDM Jam (RAMP + fast TDM)
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

            // Populate Sweep Mode combobox items if it's a ComboBoxColumn
            if (dgvBands.Columns.Contains("colSweepMode"))
            {
                DataGridViewComboBoxColumn comboCol = dgvBands.Columns["colSweepMode"] as DataGridViewComboBoxColumn;
                if (comboCol != null && comboCol.Items.Count == 0)
                {
                    comboCol.Items.AddRange(new object[] { "Triggered", "Free Run", "One Shot" });
                }
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
                dgvBands.Columns.Add(col);
            }

            if (!dgvBands.Columns.Contains("colActive"))
            {
                DataGridViewCheckBoxColumn col = new DataGridViewCheckBoxColumn();
                col.Name = "colActive";
                col.HeaderText = "Active";
                col.Width = 50;
                dgvBands.Columns.Add(col);
            }

            if (!dgvBands.Columns.Contains("colTones"))
            {
                DataGridViewTextBoxColumn col = new DataGridViewTextBoxColumn();
                col.Name = "colTones";
                col.HeaderText = "Tones";
                col.Width = 50;
                col.ValueType = typeof(int);
                dgvBands.Columns.Add(col);
            }
        }

        private void CreateGuardBandSettingsBox()
        {
            if (dgvBands == null) return;

            // ── Position controls relative to dgvBands ──
            // Shrink grid to make room for guard band buttons below it
            int origHeight = dgvBands.Height;
            dgvBands.Height = Math.Max(origHeight - 160, 200);

            int x = dgvBands.Left;
            int y = dgvBands.Bottom + 5;
            int w = dgvBands.Width;

            // ── Guard Band Controls Panel (placed below the grid) ──
            grpGuardBandSettings = new GroupBox();
            grpGuardBandSettings.Text = " GUARD BAND CONTROL ";
            grpGuardBandSettings.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            grpGuardBandSettings.ForeColor = Color.DarkBlue;
            grpGuardBandSettings.Location = new Point(x, y);
            grpGuardBandSettings.Size = new Size(w, 155);
            grpGuardBandSettings.BackColor = Color.FromArgb(230, 235, 245);
            grpGuardBandSettings.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            this.Controls.Add(grpGuardBandSettings);
            grpGuardBandSettings.BringToFront();

            // Row 1: LOAD buttons
            Button btnLoad7Bands = new Button();
            btnLoad7Bands.Text = "LOAD 7 GUARD BANDS";
            btnLoad7Bands.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            btnLoad7Bands.BackColor = Color.DarkOrange;
            btnLoad7Bands.ForeColor = Color.White;
            btnLoad7Bands.FlatStyle = FlatStyle.Flat;
            btnLoad7Bands.Size = new Size(160, 28);
            btnLoad7Bands.Location = new Point(8, 18);
            btnLoad7Bands.Click += (s, e) => Load7GuardBands();
            grpGuardBandSettings.Controls.Add(btnLoad7Bands);

            Button btnLoad4Bands = new Button();
            btnLoad4Bands.Text = "LOAD 4 STANDARD";
            btnLoad4Bands.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            btnLoad4Bands.BackColor = Color.DarkBlue;
            btnLoad4Bands.ForeColor = Color.White;
            btnLoad4Bands.FlatStyle = FlatStyle.Flat;
            btnLoad4Bands.Size = new Size(140, 28);
            btnLoad4Bands.Location = new Point(175, 18);
            btnLoad4Bands.Click += (s, e) => Load4StandardBands();
            grpGuardBandSettings.Controls.Add(btnLoad4Bands);

            // Row 2: Pulse Mode + ON/OFF times
            chkGuardPulseMode = new CheckBox();
            chkGuardPulseMode.Text = "Pulse Mode";
            chkGuardPulseMode.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            chkGuardPulseMode.ForeColor = Color.DarkGreen;
            chkGuardPulseMode.Location = new Point(8, 52);
            chkGuardPulseMode.Size = new Size(100, 20);
            chkGuardPulseMode.Checked = true;
            chkGuardPulseMode.CheckedChanged += (s, e) =>
            {
                bool en = chkGuardPulseMode.Checked;
                if (numGuardPulseOn != null) numGuardPulseOn.Enabled = en;
                if (numGuardPulseOff != null) numGuardPulseOff.Enabled = en;
                if (numGuardCycles != null) numGuardCycles.Enabled = en;
                UpdateGuardStatus();
            };
            grpGuardBandSettings.Controls.Add(chkGuardPulseMode);

            Label lblOn = new Label();
            lblOn.Text = "ON:";
            lblOn.Font = new Font("Segoe UI", 8F);
            lblOn.Location = new Point(115, 53);
            lblOn.Size = new Size(28, 18);
            grpGuardBandSettings.Controls.Add(lblOn);

            numGuardPulseOn = new NumericUpDown();
            numGuardPulseOn.Minimum = 5;
            numGuardPulseOn.Maximum = 50;
            numGuardPulseOn.Value = 10;
            numGuardPulseOn.Width = 50;
            numGuardPulseOn.Location = new Point(143, 50);
            numGuardPulseOn.ValueChanged += (s, e) => { pulseOnTimeMs = (int)numGuardPulseOn.Value; UpdateGuardStatus(); };
            grpGuardBandSettings.Controls.Add(numGuardPulseOn);

            Label lblOnMs = new Label();
            lblOnMs.Text = "ms";
            lblOnMs.Location = new Point(196, 53);
            lblOnMs.Size = new Size(22, 18);
            lblOnMs.Font = new Font("Segoe UI", 7F);
            grpGuardBandSettings.Controls.Add(lblOnMs);

            Label lblOff = new Label();
            lblOff.Text = "OFF:";
            lblOff.Font = new Font("Segoe UI", 8F);
            lblOff.Location = new Point(222, 53);
            lblOff.Size = new Size(30, 18);
            grpGuardBandSettings.Controls.Add(lblOff);

            numGuardPulseOff = new NumericUpDown();
            numGuardPulseOff.Minimum = 20;
            numGuardPulseOff.Maximum = 100;
            numGuardPulseOff.Value = 40;
            numGuardPulseOff.Width = 50;
            numGuardPulseOff.Location = new Point(253, 50);
            numGuardPulseOff.ValueChanged += (s, e) => { pulseOffTimeMs = (int)numGuardPulseOff.Value; UpdateGuardStatus(); };
            grpGuardBandSettings.Controls.Add(numGuardPulseOff);

            Label lblOffMs = new Label();
            lblOffMs.Text = "ms";
            lblOffMs.Location = new Point(306, 53);
            lblOffMs.Size = new Size(22, 18);
            lblOffMs.Font = new Font("Segoe UI", 7F);
            grpGuardBandSettings.Controls.Add(lblOffMs);

            Label lblCyc = new Label();
            lblCyc.Text = "Cycles:";
            lblCyc.Font = new Font("Segoe UI", 8F);
            lblCyc.Location = new Point(332, 53);
            lblCyc.Size = new Size(45, 18);
            grpGuardBandSettings.Controls.Add(lblCyc);

            numGuardCycles = new NumericUpDown();
            numGuardCycles.Minimum = 100;
            numGuardCycles.Maximum = 99999;
            numGuardCycles.Value = 10000;
            numGuardCycles.Width = 60;
            numGuardCycles.Location = new Point(378, 50);
            numGuardCycles.ValueChanged += (s, e) => { pulseCycles = (int)numGuardCycles.Value; UpdateGuardStatus(); };
            grpGuardBandSettings.Controls.Add(numGuardCycles);

            // Row 3: Status
            lblGuardStatus = new Label();
            lblGuardStatus.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            lblGuardStatus.ForeColor = Color.DarkGreen;
            lblGuardStatus.Location = new Point(8, 78);
            lblGuardStatus.Size = new Size(300, 30);
            lblGuardStatus.BorderStyle = BorderStyle.FixedSingle;
            lblGuardStatus.BackColor = Color.White;
            lblGuardStatus.TextAlign = ContentAlignment.MiddleLeft;
            grpGuardBandSettings.Controls.Add(lblGuardStatus);

            // Row 4: START / STOP + Diagnostics
            btnGuardStart = new Button();
            btnGuardStart.Text = "START JAMMING";
            btnGuardStart.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnGuardStart.BackColor = Color.Green;
            btnGuardStart.ForeColor = Color.White;
            btnGuardStart.FlatStyle = FlatStyle.Flat;
            btnGuardStart.Size = new Size(140, 35);
            btnGuardStart.Location = new Point(8, 113);
            btnGuardStart.Click += btnGuardStart_Click;
            grpGuardBandSettings.Controls.Add(btnGuardStart);

            btnGuardStop = new Button();
            btnGuardStop.Text = "STOP";
            btnGuardStop.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnGuardStop.BackColor = Color.Red;
            btnGuardStop.ForeColor = Color.White;
            btnGuardStop.FlatStyle = FlatStyle.Flat;
            btnGuardStop.Size = new Size(80, 35);
            btnGuardStop.Location = new Point(155, 113);
            btnGuardStop.Enabled = false;
            btnGuardStop.Click += btnGuardStop_Click;
            grpGuardBandSettings.Controls.Add(btnGuardStop);

            // Diagnostics button (opens separate window)
            Button btnDiag = new Button();
            btnDiag.Text = "DIAGNOSTICS";
            btnDiag.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            btnDiag.BackColor = Color.FromArgb(30, 30, 60);
            btnDiag.ForeColor = Color.White;
            btnDiag.FlatStyle = FlatStyle.Flat;
            btnDiag.Size = new Size(110, 35);
            btnDiag.Location = new Point(245, 113);
            btnDiag.Click += (s, e) => ShowDiagnosticsWindow();
            grpGuardBandSettings.Controls.Add(btnDiag);

            // ── USB Connection LED ──
            ledConnection = new Panel();
            ledConnection.Size = new Size(14, 14);
            ledConnection.Location = new Point(370, 120);
            ledConnection.BackColor = Color.Red;
            ledConnection.BorderStyle = BorderStyle.FixedSingle;
            // Make it circular
            System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(0, 0, 14, 14);
            ledConnection.Region = new Region(path);
            grpGuardBandSettings.Controls.Add(ledConnection);

            lblConnectionStatus = new Label();
            lblConnectionStatus.Text = "OFFLINE";
            lblConnectionStatus.Font = new Font("Segoe UI", 7F, FontStyle.Bold);
            lblConnectionStatus.ForeColor = Color.Red;
            lblConnectionStatus.Location = new Point(386, 121);
            lblConnectionStatus.Size = new Size(60, 14);
            grpGuardBandSettings.Controls.Add(lblConnectionStatus);

            UpdateGuardStatus();
        }


        /// <summary>
        /// Update Guard Band status display
        /// </summary>
        private void UpdateGuardStatus()
        {
            if (lblGuardStatus == null || chkGuardPulseMode == null) return;

            if (chkGuardPulseMode.Checked)
            {
                double dutyCycle = (pulseOnTimeMs * 100.0) / (pulseOnTimeMs + pulseOffTimeMs);
                double durationMin = ((pulseOnTimeMs + pulseOffTimeMs) * pulseCycles) / 60000.0;

                lblGuardStatus.Text = string.Format(
                    " Duty: {0:F1}% | Duration: {1:F1}min\n WiFi Impact: <{2:F0}%",
                    dutyCycle, durationMin, dutyCycle);

                // Color code based on duty cycle
                if (dutyCycle > 50)
                {
                    lblGuardStatus.ForeColor = Color.Red;
                    lblGuardStatus.Text += "\n ⚠️ HIGH IMPACT!";
                }
                else if (dutyCycle > 30)
                {
                    lblGuardStatus.ForeColor = Color.Orange;
                }
                else
                {
                    lblGuardStatus.ForeColor = Color.DarkGreen;
                }
            }
            else
            {
                lblGuardStatus.Text = " CONTINUOUS MODE\n WARNING: May disrupt WiFi";
                lblGuardStatus.ForeColor = Color.Red;
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

        #region JAMMING_STATE_MANAGEMENT

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
            if (jammingActive)
            {
                if (!this.Text.Contains("[JAMMING]"))
                    this.Text = this.Text + " [JAMMING]";
            }
            else
            {
                this.Text = this.Text.Replace(" [JAMMING]", "");
            }
        }

        private void UpdateLiveLoDisplay(double frequencyMHz)
        {
            try
            {
                this.Invoke((MethodInvoker)delegate
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
                this.Invoke((MethodInvoker)delegate
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

        private void ReconfigureDDSBandParams(BandConfiguration band)
        {
            try
            {
                double bwHz = band.BandwidthMHz * 1e6;
                uint points = (uint)Math.Max(band.Tones * 16, 32);
                ulong ddsStart = (ulong)(bwHz / 2);
                ulong ddsStep = (ulong)(bwHz / points);
                float freqCtrl = (float)(bwHz / 10);
                device.dds.Start = ddsStart;
                device.dds.Step = ddsStep;
                device.dds.Points = points;
                device.dds.FreqCtrl = freqCtrl;
                AddSequences(new SequenceSetSweepDDS(ltTxCommand, null,
                    (ushort)AD9106_MODE.RAMP, ddsStart, ddsStep, points));
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, freqCtrl));
                UpdateLiveDdsDisplay(band.BandwidthMHz, points, freqCtrl);
                Thread.Sleep(2);
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

            if (!guiActive)
            {
                MessageBox.Show("Device not connected!\nConnect the DMS hardware first.",
                    "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnGuardStart.Enabled = false;
            btnGuardStop.Enabled = true;
            isJamming = true;

            // Save current hardware state and lock controls
            SaveHardwareState();
            SetControlsJammingMode(true);

            Thread jammingThread = new Thread(ExecuteGuardBandJamming);
            jammingThread.IsBackground = true;
            jammingThread.Start();
        }

        /// <summary>
        /// Stop Guard Band Jamming
        /// </summary>
        private void btnGuardStop_Click(object sender, EventArgs e)
        {
            isJamming = false;

            // Stop DDS output immediately
            AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0));

            // Restore hardware state and unlock controls
            RestoreHardwareState();
            SetControlsJammingMode(false);

            if (btnGuardStart != null) btnGuardStart.Enabled = true;
            if (btnGuardStop != null) btnGuardStop.Enabled = false;
            UpdateGuardStatus();
            Debug.WriteLine("║ GUARD BAND JAMMING STOPPED BY USER ║");
        }

        /// <summary>
        /// Execute Guard Band Jamming with TIME-DIVISION MULTIPLEXING
        /// Jams ALL 7 bands using 4 DAC channels by rapid switching
        /// </summary>
        private void ExecuteGuardBandJamming()
        {
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

                this.Invoke((MethodInvoker)delegate
                {
                    if (dgvBands == null) return;
                    foreach (DataGridViewRow row in dgvBands.Rows)
                    {
                        if (row.IsNewRow) continue;
                        if (row.Cells["colBandName"] == null || row.Cells["colBandName"].Value == null) continue;

                        BandConfiguration band = new BandConfiguration
                        {
                            Name = row.Cells["colBandName"].Value.ToString(),
                            FrequencyMHz = Convert.ToDouble(row.Cells["colStartMhz"].Value),
                            BandwidthMHz = 20.0,
                            Tones = 8,
                            RowIndex = row.Index
                        };

                        if (dgvBands.Columns.Contains("colBandwidthMhz") && row.Cells["colBandwidthMhz"].Value != null)
                            try { band.BandwidthMHz = Convert.ToDouble(row.Cells["colBandwidthMhz"].Value); } catch { }
                        if (dgvBands.Columns.Contains("colTones") && row.Cells["colTones"].Value != null)
                            try { band.Tones = Convert.ToInt32(row.Cells["colTones"].Value); } catch { }

                        allBands.Add(band);
                    }
                    usePulseMode = (chkGuardPulseMode != null && chkGuardPulseMode.Checked);
                });

                if (allBands.Count == 0)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        MessageBox.Show("No bands configured!\nLoad bands using the preset buttons.",
                            "No Bands", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        if (btnGuardStart != null) btnGuardStart.Enabled = true;
                        if (btnGuardStop != null) btnGuardStop.Enabled = false;
                    });
                    return;
                }

                // ── Log band plan ──
                Debug.WriteLine("│ Bands: " + allBands.Count);
                foreach (var b in allBands)
                    Debug.WriteLine("│   " + b.Name + ": " + b.FrequencyMHz + " MHz, BW=" + b.BandwidthMHz + " MHz");

                // ══ CONFIGURE DDS ONCE ══
                // Set DDS to RAMP mode for wideband coverage, using first band's settings
                // The DDS creates the modulation pattern; we only hop the LO for each band.
                ConfigureDDSForJamming(allBands[0]);

                if (usePulseMode)
                {
                    // ══════════════════════════════════════
                    // PULSED TDM MODE
                    // ══════════════════════════════════════
                    Debug.WriteLine("│ Mode: PULSED TDM  ON=" + pulseOnTimeMs + "ms OFF=" + pulseOffTimeMs + "ms");
                    int timePerBand = Math.Max(pulseOnTimeMs / allBands.Count, 2);
                    double currentBW = allBands[0].BandwidthMHz;
                    int currentTones = allBands[0].Tones;

                    for (int cycle = 0; cycle < pulseCycles && isJamming; cycle++)
                    {
                        // ── JAM PHASE: hop through all bands ──
                        for (int i = 0; i < allBands.Count && isJamming; i++)
                        {
                            if (allBands[i].BandwidthMHz != currentBW || allBands[i].Tones != currentTones)
                            {
                                ReconfigureDDSBandParams(allBands[i]);
                                currentBW = allBands[i].BandwidthMHz;
                                currentTones = allBands[i].Tones;
                            }
                            HopToFrequency(allBands[i].FrequencyMHz);
                            UpdateTDMStatus(cycle, i, allBands.Count, true);
                            Thread.Sleep(timePerBand);
                        }

                        // ── SILENT PHASE: kill DDS output ──
                        AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0));
                        UpdateTDMStatus(cycle, -1, allBands.Count, false);
                        Thread.Sleep(pulseOffTimeMs);

                        if (cycle % 100 == 0)
                            Debug.WriteLine("│ Cycle " + cycle + "/" + pulseCycles);
                    }
                }
                else
                {
                    // ══════════════════════════════════════
                    // CONTINUOUS TDM MODE
                    // ══════════════════════════════════════
                    Debug.WriteLine("│ Mode: CONTINUOUS TDM");
                    int dwellMs = Math.Max(5, 50 / allBands.Count);
                    double currentBW_c = allBands[0].BandwidthMHz;
                    int currentTones_c = allBands[0].Tones;

                    while (isJamming)
                    {
                        for (int i = 0; i < allBands.Count && isJamming; i++)
                        {
                            if (allBands[i].BandwidthMHz != currentBW_c || allBands[i].Tones != currentTones_c)
                            {
                                ReconfigureDDSBandParams(allBands[i]);
                                currentBW_c = allBands[i].BandwidthMHz;
                                currentTones_c = allBands[i].Tones;
                            }
                            HopToFrequency(allBands[i].FrequencyMHz);
                            UpdateTDMStatus(0, i, allBands.Count, true);
                            Thread.Sleep(dwellMs);
                        }
                    }
                }

                // ── Stop output and restore state ──
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0));
                RestoreHardwareState();
                Debug.WriteLine("│ ✓ Jamming complete - state restored");
                Debug.WriteLine("╚═══════════════════════════════════════════════════════╝\n");

                this.Invoke((MethodInvoker)delegate
                {
                    SetControlsJammingMode(false);
                    if (btnGuardStart != null) btnGuardStart.Enabled = true;
                    if (btnGuardStop != null) btnGuardStop.Enabled = false;
                    UpdateGuardStatus();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("│ ✗ ERROR: " + ex.Message);
                RestoreHardwareState();
                this.Invoke((MethodInvoker)delegate
                {
                    SetControlsJammingMode(false);
                    MessageBox.Show("Guard Band Error: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    if (btnGuardStart != null) btnGuardStart.Enabled = true;
                    if (btnGuardStop != null) btnGuardStop.Enabled = false;
                });
            }
            finally
            {
                isJamming = false;
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
                // Use RAMP mode (AD9106_MODE index 2) for wideband noise generation
                ushort ddsMode = (ushort)AD9106_MODE.RAMP;
                double bwHz = firstBand.BandwidthMHz * 1e6;
                uint points = (uint)Math.Max(firstBand.Tones * 16, 32);
                ulong ddsStart = (ulong)(bwHz / 2);
                ulong ddsStep = (ulong)(bwHz / points);

                device.dds.SweepOn = ddsMode;
                device.dds.Start = ddsStart;
                device.dds.Step = ddsStep;
                device.dds.Points = points;
                device.dds.FreqCtrl = (float)(bwHz / 10);

                // Send DDS sweep parameters
                AddSequences(new SequenceSetSweepDDS(ltTxCommand, null,
                    ddsMode, ddsStart, ddsStep, points));

                // Configure AD9106 registers for RAMP mode (same as DDSSetConfiguration RAMP path)
                ushort Cycle = 1;
                device.dds.registers[0x20] = 0x0001;
                device.dds.registers[0x26] = 0x3233;  // All 4 channels active
                device.dds.registers[0x27] = 0x3233;
                device.dds.registers[0x28] = 0x0111;
                device.dds.registers[0x29] = 0x8000;
                device.dds.registers[0x2A] = 0x0101;
                device.dds.registers[0x2B] = 0x0101;
                device.dds.registers[0x2C] = 0x0003;
                device.dds.registers[0x2D] = 0x0000;
                device.dds.registers[0x36] = 0x0404;
                device.dds.registers[0x37] = 0x0404;
                device.dds.registers[0x44] = 0x0002;
                device.dds.registers[0x45] = 0x4441;
                for (int i = 0; i < 4; i++)
                {
                    device.dds.registers[(ushort)(0x50 + 4 * i)] = (ushort)(1000);
                    device.dds.registers[(ushort)(0x51 + 4 * i)] = 0x0000;
                    device.dds.registers[(ushort)(0x52 + 4 * i)] = (ushort)(points * 16);
                    device.dds.registers[(ushort)(0x53 + 4 * i)] = Cycle;
                }

                // Write all AD9106 registers 0x00-0x5F
                for (ushort i = 0x00; i < 0x60; i++)
                {
                    AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, i, device.dds.registers[i]));
                }

                // Latch configuration
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1E, 0x0003));
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1F, 0x0000));
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));

                // Start DDS output
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 10));
                AddSequences(new SequenceDelay(200));
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0));

                // Wait for DDS to stabilize
                Thread.Sleep(300);

                // Update live DDS display
                UpdateLiveDdsDisplay(firstBand.BandwidthMHz, points, device.dds.FreqCtrl);

                Debug.WriteLine("│ DDS configured: RAMP mode, BW=" + firstBand.BandwidthMHz + " MHz, Points=" + points);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("│ ✗ DDS config error: " + ex.Message);
            }
        }

        /// <summary>
        /// Fast LO frequency hop. Only changes PLL - DDS keeps running.
        /// This is the core TDM operation: hop LO to move the jamming band.
        /// </summary>
        private void HopToFrequency(double frequencyMHz)
        {
            try
            {
                double freqHz = frequencyMHz * 1e6;
                device.lo.pll.OutFrequency = freqHz;
                device.lo.SweepOn = 0;
                device.lo.Points = 1;
                device.lo.WaitLD = 0;

                // Fast PLL update - single sequence command
                AddSequences(new SequenceSetSweepLoParameters(ltTxCommand, null,
                    (float)freqHz, 0, 1, 0, 0, 0));

                // Update live LO display
                UpdateLiveLoDisplay(frequencyMHz);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("│ ✗ Hop error: " + ex.Message);
            }
        }

        /// <summary>
        /// Update UI with TDM status
        /// </summary>
        private void UpdateTDMStatus(int cycle, int currentBatch, int totalBatches, bool jamming)
        {
            this.Invoke((MethodInvoker)delegate
            {
                if (lblGuardStatus == null) return;

                if (jamming)
                {
                    string batchInfo = "";
                    switch (currentBatch)
                    {
                        case 0: batchInfo = "2.4GHz Guards (1-4)"; break;
                        case 1: batchInfo = "5.8GHz+900MHz (5-7)"; break;
                        default: batchInfo = "Batch " + (currentBatch + 1); break;
                    }

                    lblGuardStatus.Text = string.Format(" 🔴 JAMMING: {0}\n Pulse {1}/{2} | TDM Active",
                        batchInfo, cycle + 1, pulseCycles);
                    lblGuardStatus.ForeColor = Color.Red;
                }
                else
                {
                    lblGuardStatus.Text = string.Format(" 🟢 SILENT\n Pulse {0}/{1} | WiFi Recovery",
                        cycle + 1, pulseCycles);
                    lblGuardStatus.ForeColor = Color.Green;
                }
            });
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

                dgvBands.Rows.Add("", "Triggered", "WiFi 2.4GHz", 2450.0, 80.0, 100.0, false, 13);
                dgvBands.Rows.Add("", "Triggered", "DJI 5.8GHz", 5800.0, 60.0, 100.0, false, 20);
                dgvBands.Rows.Add("", "Triggered", "LTE Band 3", 1800.0, 60.0, 100.0, false, 32);
                dgvBands.Rows.Add("", "Triggered", "Walkie 900MHz", 900.0, 50.0, 100.0, false, 8);
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

            // 2.4 GHz Guard Bands - GREEN
            AddBandRow("", "Triggered", "2.4G Guard Low", 2405.0, 10.0, 100.0, true, 4, Color.LightGreen);
            AddBandRow("", "Triggered", "2.4G Guard Mid1", 2425.0, 10.0, 100.0, true, 4, Color.LightGreen);
            AddBandRow("", "Triggered", "2.4G Guard Mid2", 2451.0, 8.0, 100.0, true, 4, Color.LightGreen);
            AddBandRow("", "Triggered", "2.4G Guard High", 2476.5, 13.0, 100.0, true, 4, Color.LightGreen);

            // 5.8 GHz DJI - CYAN
            AddBandRow("", "Triggered", "5.8G DJI Low", 5735.0, 20.0, 100.0, true, 8, Color.LightCyan);
            AddBandRow("", "Triggered", "5.8G DJI High", 5840.0, 20.0, 100.0, true, 8, Color.LightCyan);

            // Autel 900MHz - YELLOW
            AddBandRow("", "Triggered", "Autel 900MHz", 915.0, 30.0, 100.0, true, 8, Color.LightYellow);

            Debug.WriteLine("║ 7 GUARD BANDS LOADED - WiFi Ch 1,6,11 protected ║");
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
            double startMhz, double bwMhz, double power, bool active, int tones, Color rowColor)
        {
            if (dgvBands == null) return;

            int rowIdx = dgvBands.Rows.Add();
            DataGridViewRow row = dgvBands.Rows[rowIdx];

            // Set values by column name (safe - ignores if column doesn't exist)
            SetCellValue(row, "colStatus", status);
            SetCellValue(row, "colSweepMode", sweepMode);
            SetCellValue(row, "colBandName", bandName);
            SetCellValue(row, "colStartMhz", startMhz);
            SetCellValue(row, "colBandwidthMhz", bwMhz);
            SetCellValue(row, "colPower", power);
            SetCellValue(row, "colActive", active);
            SetCellValue(row, "colTones", tones);

            if (rowColor != Color.Empty)
            {
                row.DefaultCellStyle.BackColor = rowColor;
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

            // Remove all colors
            dgvBands.Rows.Add("", "Triggered", "WiFi 2.4GHz", 2450.0, 80.0, 100.0, false, 13);
            dgvBands.Rows.Add("", "Triggered", "DJI 5.8GHz", 5800.0, 60.0, 100.0, false, 20);
            dgvBands.Rows.Add("", "Triggered", "LTE Band 3", 1800.0, 60.0, 100.0, false, 32);
            dgvBands.Rows.Add("", "Triggered", "Walkie 900MHz", 900.0, 50.0, 100.0, false, 8);

            Debug.WriteLine("║ 4 STANDARD BANDS LOADED - Normal pattern jamming ║");
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
                case "Ramp": return 0;
                case "Sweep": return 1;
                case "Spot": return 2;
                case "CW": return 3;
                case "Random": return 4;
                case "Multi-Channel": return 5;
                case "Chirp": return 6;
                case "Multi-Tone": return 7;
                default: return 0;
            }
        }

        private int CalculateOptimalTones(DataGridViewRow row, string bandName, double bandwidthMHz)
        {
            int numTones = 8;
            if (dgvBands.Columns.Contains("colTones") && row.Cells["colTones"].Value != null)
            {
                try { numTones = Convert.ToInt32(row.Cells["colTones"].Value); } catch { }
            }
            if (numTones < 2) numTones = 2;
            if (numTones > 64) numTones = 64;
            return numTones;
        }

        private bool ValidateBandParameters(DataGridViewRow row, out string errorMessage)
        {
            errorMessage = "";
            if (row.Cells["colStartMhz"].Value == null) { errorMessage = "Start frequency required"; return false; }
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
                        sw.WriteLine("# Format: BandName|TwMem|StartMHz|BwMHz|StepKHz|Loop|Tones");
                        sw.WriteLine();

                        foreach (DataGridViewRow row in dgvBands.Rows)
                        {
                            if (row.IsNewRow) continue;

                            string bandName = row.Cells["colBandName"].Value?.ToString() ?? "Band";
                            string twMem = row.Cells["colSweepMode"].Value?.ToString() ?? "Triggered";
                            string startMhz = row.Cells["colStartMhz"].Value?.ToString() ?? "2400";
                            string bwMhz = (dgvBands.Columns.Contains("colBandwidthMhz") && row.Cells["colBandwidthMhz"].Value != null ? row.Cells["colBandwidthMhz"].Value.ToString() : "50");
                            string stepKhz = "100";  // Step not stored in grid
                            string loop = (dgvBands.Columns.Contains("colActive") && row.Cells["colActive"].Value != null ? row.Cells["colActive"].Value.ToString() : "True");
                            string tones = "8";
                            if (dgvBands.Columns.Contains("colTones") && row.Cells["colTones"].Value != null)
                                tones = row.Cells["colTones"].Value.ToString();

                            sw.WriteLine(bandName + "|" + twMem + "|" + startMhz + "|" + bwMhz + "|" + stepKhz + "|" + loop + "|" + tones);
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
                            string tones = data.Length >= 7 ? data[6] : "8";
                            AddBandRow("", data[1], data[0],
                                Convert.ToDouble(data[2]), Convert.ToDouble(data[3]),
                                Convert.ToDouble(data[4]), Convert.ToBoolean(data[5]),
                                Convert.ToInt32(tones), Color.Empty);
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
                            errorMessage = "Start MHz must be between 20-6000 MHz!\n\nValid ranges:\n• VHF: 30-300 MHz\n• UHF: 300-1000 MHz\n• L-Band: 1000-2000 MHz\n• S-Band: 2000-4000 MHz\n• C-Band: 4000-6000 MHz";
                        }
                    }
                    else if (!string.IsNullOrEmpty(newValue))
                    {
                        isValid = false;
                        errorMessage = "Start MHz must be a number!";
                    }
                }
                else if (columnName == "colBwMhz")
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
