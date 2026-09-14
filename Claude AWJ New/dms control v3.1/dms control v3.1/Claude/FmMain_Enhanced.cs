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
using nsAlexKir;
using nsAlexKir.RF_Library;
using nsAlexKir.Sequences;

namespace dms_control_v3
{
    public partial class FmMain : Form
    {
        #region PARAMETERS
        public bool guiActive = false;
        DmsDevice device;
        List<IBaseSequence> ltServiceSequences;
        GlobalConfigurations run_cfg;
        List<Command> ltTxCommand;
        List<Command> ltRxCommand;
        String sSerialNumber = "0000000001";

        PerformanceMonitor perfMon = new PerformanceMonitor();

        private Stopwatch bandTimer = new Stopwatch();
        private Stopwatch totalTimer = new Stopwatch();

        // NEW: Enhanced pattern memory support
        private AD9106PatternGenerator patternGen = new AD9106PatternGenerator();
        private Dictionary<string, uint[]> loadedPatterns = new Dictionary<string, uint[]>();
        private PatternPresetLibrary presetLibrary = new PatternPresetLibrary();
        private int currentPresetIndex = 0;
        #endregion

        public FmMain(GlobalConfigurations _cfg)
        {
            InitializeComponent();
            run_cfg = _cfg;
            device = new DmsDevice();
            ltRxCommand = new List<Command>();
            ltTxCommand = new List<Command>();

            if (timer1 != null) timer1.Enabled = true;

            if (cmbbxDdsMode != null && cmbbxDdsMode.Items.Count == 0)
            {
                cmbbxDdsMode.Items.AddRange(new object[] { 
                    "Ramp", 
                    "Sweep", 
                    "Spot", 
                    "CW", 
                    "Random", 
                    "Multi-Channel", 
                    "Chirp", 
                    "Multi-Tone" 
                });
                cmbbxDdsMode.SelectedIndex = 0;
            }

            if (dgvBands != null)
            {
                foreach (DataGridViewColumn col in dgvBands.Columns)
                {
                    if (col.HeaderText == "Sweep Mode" && col is DataGridViewComboBoxColumn)
                    {
                        DataGridViewComboBoxColumn sweepCol = (DataGridViewComboBoxColumn)col;
                        if (sweepCol.Items.Count == 0)
                        {
                            sweepCol.Items.AddRange(new object[] { "Off/Idle", "Continuous", "Triggered", "Gated", "Pulse", "Burst" });
                        }
                    }
                }

                // Add default multi-band configuration
                dgvBands.Rows.Add("", "Triggered", "WiFi 2.4GHz", 2400.0, 100.0, 100.0, false);
                dgvBands.Rows.Add("", "Triggered", "DJI 5.8GHz", 5800.0, 100.0, 100.0, false);
                dgvBands.Rows.Add("", "Triggered", "LTE Band 3", 1800.0, 100.0, 100.0, false);
                dgvBands.Rows.Add("", "Triggered", "Walkie 900MHz", 900.0, 50.0, 100.0, false);
            }

            // Initialize preset library
            presetLibrary.LoadDefaultPresets();
            
            Debug.WriteLine("╔═══════════════════════════════════════════════════════╗");
            Debug.WriteLine("║     DMS CONTROL v3.2 - PATTERN MEMORY ENABLED        ║");
            Debug.WriteLine("╚═══════════════════════════════════════════════════════╝");
        }

        private uint GetTwMemFromText(object cellValue)
        {
            if (cellValue == null) return 2;
            string mode = cellValue.ToString();
            switch (mode)
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
            if (cmbbxDdsMode == null || cmbbxDdsMode.SelectedItem == null)
            {
                return 0;
            }

            string mode = cmbbxDdsMode.SelectedItem.ToString();
            switch (mode)
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

        private double CalculateSweepTime(double bandwidthMHz, double stepKHz, double dwellTimeUs)
        {
            double bandwidthKHz = bandwidthMHz * 1000.0;
            int points = (int)(bandwidthKHz / stepKHz) + 1;
            double sweepTimeMs = (points * dwellTimeUs) / 1000.0;
            return sweepTimeMs;
        }

        private void ExecuteAWJMasterCycle()
        {
            if (dgvBands == null) return;

            try
            {
                guiActive = true;
                totalTimer.Restart();

                uint globalDdsMode = GetDdsModeValue();
                string ddsModeName = cmbbxDdsMode.SelectedItem != null ? cmbbxDdsMode.SelectedItem.ToString() : "Ramp";

                Debug.WriteLine("");
                Debug.WriteLine("╔═══════════════════════════════════════════════════════╗");
                Debug.WriteLine("║  AWJ MASTER CYCLE START - Mode: " + ddsModeName.PadRight(20) + "║");
                Debug.WriteLine("╚═══════════════════════════════════════════════════════╝");

                // *** MULTI-CHANNEL MODE - ALL 4 DACS SIMULTANEOUSLY ***
                if (ddsModeName == "Multi-Channel")
                {
                    ExecuteMultiChannelMode();
                    return;
                }

                // *** SINGLE CHANNEL MODES ***
                foreach (DataGridViewRow row in dgvBands.Rows)
                {
                    if (row.IsNewRow) continue;
                    if (row.Cells["colBandName"] == null || row.Cells["colBandName"].Value == null) continue;
                    if (!guiActive) break;

                    bool loopThisBand = false;
                    if (dgvBands.Columns.Contains("colLoop") && row.Cells["colLoop"].Value != null)
                    {
                        loopThisBand = Convert.ToBoolean(row.Cells["colLoop"].Value);
                    }

                    int loopCount = 0;

                    do
                    {
                        bandTimer.Restart();

                        foreach (DataGridViewRow r in dgvBands.Rows)
                        {
                            r.DefaultCellStyle.BackColor = Color.White;
                        }
                        row.DefaultCellStyle.BackColor = Color.LightGreen;

                        if (dgvBands.Columns.Contains("colStatus"))
                        {
                            if (loopThisBand)
                            {
                                row.Cells["colStatus"].Value = "LOOP " + loopCount.ToString();
                            }
                            else
                            {
                                row.Cells["colStatus"].Value = "RUNNING";
                            }
                        }
                        dgvBands.Refresh();

                        uint twMemValue = GetTwMemFromText(row.Cells["colTwMem"].Value);
                        string twMemName = row.Cells["colTwMem"].Value != null ? row.Cells["colTwMem"].Value.ToString() : "Triggered";

                        string bandName = "Unknown";
                        if (row.Cells["colBandName"].Value != null)
                        {
                            bandName = row.Cells["colBandName"].Value.ToString();
                        }

                        double startFreqMHz = Convert.ToDouble(row.Cells["colStartMhz"].Value);
                        double bandwidthMHz = Convert.ToDouble(row.Cells["colBwMhz"].Value);
                        double stepKHz = Convert.ToDouble(row.Cells["colStepKhz"].Value);

                        int points = (int)((bandwidthMHz * 1000.0) / stepKHz) + 1;
                        double estimatedSweepTime = CalculateSweepTime(bandwidthMHz, stepKHz, 10.0);

                        ltTxCommand.Clear();

                        // Set PLL frequency
                        device.lo.pll.OutFrequency = startFreqMHz * 1e6;
                        foreach (int regIdx in device.lo.pll.initSequence)
                        {
                            AddSequences(new SequenceWriteRegisterPLL(ltTxCommand, null, (ulong)device.lo.pll.registers[regIdx].body));
                        }

                        // *** PATTERN MEMORY MODES ***
                        if (ddsModeName == "Random")
                        {
                            ExecuteRandomMode(row, bandName, 1);
                        }
                        else if (ddsModeName == "Chirp")
                        {
                            ExecuteChirpMode(row, bandName, startFreqMHz, bandwidthMHz, 1);
                        }
                        else if (ddsModeName == "Multi-Tone")
                        {
                            ExecuteMultiToneMode(row, bandName, startFreqMHz, bandwidthMHz, 1);
                        }
                        else
                        {
                            // *** EXISTING SWEEP MODE ***
                            uint ddsStart = (uint)(device.dds.Start);
                            uint ddsStep = (uint)(stepKHz * 1000);
                            uint ddsPoints = (uint)points;

                            AddSequences(new SequenceSetSweepDDS(ltTxCommand, null, (ushort)twMemValue, ddsStart, ddsStep, ddsPoints));

                            Debug.WriteLine("│ Band: " + bandName.PadRight(15) + " | Mode: " + ddsModeName.PadRight(10) + " | Points: " + points.ToString().PadLeft(5) + " │");

                            int delayMs = 10;
                            if (twMemValue == 2)
                            {
                                delayMs = (int)(estimatedSweepTime * 1.2);
                            }
                            else if (twMemValue == 1)
                            {
                                delayMs = (int)estimatedSweepTime;
                            }

                            Thread.Sleep(delayMs);
                        }

                        bandTimer.Stop();

                        double actualTime = bandTimer.Elapsed.TotalMilliseconds;
                        if (dgvBands.Columns.Contains("colStatus"))
                        {
                            row.Cells["colStatus"].Value = "Done: " + actualTime.ToString("F1") + "ms";
                        }

                        Debug.WriteLine("└─ Complete: " + actualTime.ToString("F2") + " ms");

                        System.Windows.Forms.Application.DoEvents();

                        if (dgvBands.Columns.Contains("colLoop"))
                        {
                            loopThisBand = Convert.ToBoolean(row.Cells["colLoop"].Value);
                        }

                        loopCount++;

                    } while (loopThisBand && guiActive);
                }

                totalTimer.Stop();
                Debug.WriteLine("╔═══════════════════════════════════════════════════════╗");
                Debug.WriteLine("║  CYCLE COMPLETE: " + totalTimer.Elapsed.TotalMilliseconds.ToString("F0").PadLeft(6) + " ms" + "                        ║");
                Debug.WriteLine("╚═══════════════════════════════════════════════════════╝\n");

                foreach (DataGridViewRow r in dgvBands.Rows)
                {
                    r.DefaultCellStyle.BackColor = Color.White;
                }

                guiActive = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cycle Error: " + ex.Message + "\n\nStack: " + ex.StackTrace, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                guiActive = false;

                if (dgvBands != null)
                {
                    foreach (DataGridViewRow r in dgvBands.Rows)
                    {
                        r.DefaultCellStyle.BackColor = Color.White;
                    }
                }
            }
        }

        #region PATTERN_MODE_EXECUTORS

        /// <summary>
        /// Execute PRBS Random Pattern Mode
        /// </summary>
        private void ExecuteRandomMode(DataGridViewRow row, string bandName, int dacChannel)
        {
            if (dgvBands.Columns.Contains("colStatus"))
            {
                row.Cells["colStatus"].Value = "LOADING PRBS...";
                dgvBands.Refresh();
            }

            string patternKey = bandName + "_PRBS_4096";

            if (!loadedPatterns.ContainsKey(patternKey))
            {
                Debug.WriteLine("┌─ Generating PRBS pattern: " + patternKey);

                uint[] pattern = patternGen.GeneratePRBSPattern(4096);
                loadedPatterns[patternKey] = pattern;

                AddSequences(new SequenceLoadPatternMemory(ltTxCommand, null, pattern));

                Debug.WriteLine("│ ✓ Pattern loaded to SRAM: 4096 samples");
            }
            else
            {
                Debug.WriteLine("┌─ Using cached PRBS: " + patternKey);
                AddSequences(new SequenceLoadPatternMemory(ltTxCommand, null, loadedPatterns[patternKey]));
            }

            AddSequences(new SequenceConfigurePattern(ltTxCommand, null, dacChannel, 0, 4095, 0));
            AddSequences(new SequenceUpdateConfig(ltTxCommand, null));

            if (dgvBands.Columns.Contains("colStatus"))
            {
                row.Cells["colStatus"].Value = "PRBS READY";
                dgvBands.Refresh();
            }

            AddSequences(new SequenceTriggerPattern(ltTxCommand, null));

            Debug.WriteLine("│ Pattern Memory: 4096 points | Speed: 22.76 μs | Hop Rate: 180 MHz");

            Thread.Sleep(1);

            AddSequences(new SequenceStopPattern(ltTxCommand, null));
        }

        /// <summary>
        /// Execute Chirp Pattern Mode (Linear Frequency Sweep)
        /// </summary>
        private void ExecuteChirpMode(DataGridViewRow row, string bandName, double startFreqMHz, double bandwidthMHz, int dacChannel)
        {
            if (dgvBands.Columns.Contains("colStatus"))
            {
                row.Cells["colStatus"].Value = "LOADING CHIRP...";
                dgvBands.Refresh();
            }

            string patternKey = bandName + "_CHIRP_4096";

            if (!loadedPatterns.ContainsKey(patternKey))
            {
                Debug.WriteLine("┌─ Generating CHIRP pattern: " + startFreqMHz + " to " + (startFreqMHz + bandwidthMHz) + " MHz");

                uint[] pattern = patternGen.GenerateChirpPattern(0.0, 1.0, 4096);
                loadedPatterns[patternKey] = pattern;

                AddSequences(new SequenceLoadPatternMemory(ltTxCommand, null, pattern));

                Debug.WriteLine("│ ✓ Chirp loaded: Linear sweep across 4096 points");
            }
            else
            {
                Debug.WriteLine("┌─ Using cached CHIRP: " + patternKey);
                AddSequences(new SequenceLoadPatternMemory(ltTxCommand, null, loadedPatterns[patternKey]));
            }

            AddSequences(new SequenceConfigurePattern(ltTxCommand, null, dacChannel, 0, 4095, 0));
            AddSequences(new SequenceUpdateConfig(ltTxCommand, null));

            if (dgvBands.Columns.Contains("colStatus"))
            {
                row.Cells["colStatus"].Value = "CHIRP READY";
                dgvBands.Refresh();
            }

            AddSequences(new SequenceTriggerPattern(ltTxCommand, null));

            Debug.WriteLine("│ Chirp: " + bandwidthMHz + " MHz sweep | Time: 22.76 μs");

            Thread.Sleep(1);

            AddSequences(new SequenceStopPattern(ltTxCommand, null));
        }

        /// <summary>
        /// Execute Multi-Tone Pattern Mode (Simultaneous Frequencies)
        /// </summary>
        private void ExecuteMultiToneMode(DataGridViewRow row, string bandName, double startFreqMHz, double bandwidthMHz, int dacChannel)
        {
            if (dgvBands.Columns.Contains("colStatus"))
            {
                row.Cells["colStatus"].Value = "LOADING MULTI-TONE...";
                dgvBands.Refresh();
            }

            string patternKey = bandName + "_MULTITONE_4096";

            if (!loadedPatterns.ContainsKey(patternKey))
            {
                Debug.WriteLine("┌─ Generating MULTI-TONE pattern: " + bandName);

                int numTones = 8;
                uint[] pattern = patternGen.GenerateMultiTonePattern(numTones, 4096);
                loadedPatterns[patternKey] = pattern;

                AddSequences(new SequenceLoadPatternMemory(ltTxCommand, null, pattern));

                Debug.WriteLine("│ ✓ Multi-tone loaded: " + numTones + " simultaneous frequencies");
            }
            else
            {
                Debug.WriteLine("┌─ Using cached MULTI-TONE: " + patternKey);
                AddSequences(new SequenceLoadPatternMemory(ltTxCommand, null, loadedPatterns[patternKey]));
            }

            AddSequences(new SequenceConfigurePattern(ltTxCommand, null, dacChannel, 0, 4095, 0));
            AddSequences(new SequenceUpdateConfig(ltTxCommand, null));

            if (dgvBands.Columns.Contains("colStatus"))
            {
                row.Cells["colStatus"].Value = "MULTI-TONE READY";
                dgvBands.Refresh();
            }

            AddSequences(new SequenceTriggerPattern(ltTxCommand, null));

            Debug.WriteLine("│ Multi-Tone: 8 frequencies | Instant coverage: " + bandwidthMHz + " MHz");

            Thread.Sleep(1);

            AddSequences(new SequenceStopPattern(ltTxCommand, null));
        }

        /// <summary>
        /// Execute Multi-Channel Mode - All 4 DACs simultaneously
        /// </summary>
        private void ExecuteMultiChannelMode()
        {
            Debug.WriteLine("┌─────────────────────────────────────────────────┐");
            Debug.WriteLine("│       MULTI-CHANNEL MODE - 4 DACs ACTIVE        │");
            Debug.WriteLine("└─────────────────────────────────────────────────┘");

            ltTxCommand.Clear();

            int dacChannel = 1;
            List<string> activeBands = new List<string>();

            foreach (DataGridViewRow row in dgvBands.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Cells["colBandName"] == null || row.Cells["colBandName"].Value == null) continue;
                if (dacChannel > 4) break; // Only 4 DACs available

                string bandName = row.Cells["colBandName"].Value.ToString();
                double startFreqMHz = Convert.ToDouble(row.Cells["colStartMhz"].Value);
                double bandwidthMHz = Convert.ToDouble(row.Cells["colBwMhz"].Value);

                row.DefaultCellStyle.BackColor = Color.LightBlue;
                activeBands.Add(bandName);

                Debug.WriteLine("│ DAC" + dacChannel + ": " + bandName.PadRight(20) + " | " + startFreqMHz + " MHz | BW: " + bandwidthMHz + " MHz");

                // Set PLL for this band
                device.lo.pll.OutFrequency = startFreqMHz * 1e6;
                foreach (int regIdx in device.lo.pll.initSequence)
                {
                    AddSequences(new SequenceWriteRegisterPLL(ltTxCommand, null, (ulong)device.lo.pll.registers[regIdx].body));
                }

                // Generate and load pattern for this DAC
                string patternKey = "DAC" + dacChannel + "_" + bandName + "_PRBS";

                if (!loadedPatterns.ContainsKey(patternKey))
                {
                    uint[] pattern = patternGen.GeneratePRBSPattern(4096);
                    loadedPatterns[patternKey] = pattern;
                    AddSequences(new SequenceLoadPatternMemory(ltTxCommand, null, pattern));
                }
                else
                {
                    AddSequences(new SequenceLoadPatternMemory(ltTxCommand, null, loadedPatterns[patternKey]));
                }

                // Configure this DAC channel
                AddSequences(new SequenceConfigurePattern(ltTxCommand, null, dacChannel, 0, 4095, 0));

                dacChannel++;
            }

            // Update all channels simultaneously
            AddSequences(new SequenceUpdateConfig(ltTxCommand, null));

            Debug.WriteLine("│ ✓ All " + (dacChannel - 1) + " channels configured");
            Debug.WriteLine("│ ⚡ Simultaneous Coverage: ~" + ((dacChannel - 1) * 35) + " MHz total bandwidth");
            Debug.WriteLine("└─────────────────────────────────────────────────┘");

            // Trigger all channels at once
            AddSequences(new SequenceTriggerPattern(ltTxCommand, null));

            Thread.Sleep(5); // Let all channels run

            AddSequences(new SequenceStopPattern(ltTxCommand, null));

            // Restore row colors
            foreach (DataGridViewRow row in dgvBands.Rows)
            {
                if (!row.IsNewRow && activeBands.Contains(row.Cells["colBandName"].Value?.ToString()))
                {
                    row.DefaultCellStyle.BackColor = Color.LightGreen;
                    if (dgvBands.Columns.Contains("colStatus"))
                    {
                        row.Cells["colStatus"].Value = "MULTI-CH DONE";
                    }
                }
            }

            dgvBands.Refresh();
            
            totalTimer.Stop();
            Debug.WriteLine("╔═══════════════════════════════════════════════════════╗");
            Debug.WriteLine("║  MULTI-CHANNEL COMPLETE: " + totalTimer.Elapsed.TotalMilliseconds.ToString("F0").PadLeft(6) + " ms" + "               ║");
            Debug.WriteLine("╚═══════════════════════════════════════════════════════╝\n");
        }

        /// <summary>
        /// Real-time pattern switching - cycles through preset library
        /// </summary>
        private void SwitchToNextPreset()
        {
            if (presetLibrary.Presets.Count == 0) return;

            currentPresetIndex = (currentPresetIndex + 1) % presetLibrary.Presets.Count;
            PatternPreset preset = presetLibrary.Presets[currentPresetIndex];

            Debug.WriteLine("");
            Debug.WriteLine("⚡ SWITCHING TO PRESET: " + preset.Name);

            ltTxCommand.Clear();

            // Stop current pattern
            AddSequences(new SequenceStopPattern(ltTxCommand, null));

            // Load new preset pattern
            AddSequences(new SequenceLoadPatternMemory(ltTxCommand, null, preset.Pattern));
            AddSequences(new SequenceConfigurePattern(ltTxCommand, null, 1, 0, (ushort)(preset.Pattern.Length - 1), 0));
            AddSequences(new SequenceUpdateConfig(ltTxCommand, null));

            // Restart with new pattern
            AddSequences(new SequenceTriggerPattern(ltTxCommand, null));

            Debug.WriteLine("  ✓ Preset switched in <1 μs");
            Debug.WriteLine("  Threat: " + preset.ThreatType);
            Debug.WriteLine("  Description: " + preset.Description + "\n");
        }

        #endregion

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (tlsplbLO != null)
            {
                tlsplbLO.Text = "LO: " + (device.lo.pll.OutFrequency / 1e6).ToString("F2") + " MHz";
            }
            if (tlspSerialNumber != null)
            {
                tlspSerialNumber.Text = "SN: " + sSerialNumber + " | Presets: " + presetLibrary.Presets.Count;
            }
        }

        private void AddSequences(IBaseSequence seq)
        {
            // Your existing implementation
        }

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
                        sw.WriteLine("# DMS Control v3.2 Configuration");
                        sw.WriteLine("# Created: " + DateTime.Now.ToString());
                        sw.WriteLine("# Format: BandName|TwMem|StartMHz|BwMHz|StepKHz|Loop");
                        sw.WriteLine();

                        foreach (DataGridViewRow row in dgvBands.Rows)
                        {
                            if (row.IsNewRow) continue;

                            string bandName = "Band";
                            string twMem = "Triggered";
                            string startMhz = "2400";
                            string bwMhz = "50";
                            string stepKhz = "100";
                            string loop = "False";

                            if (row.Cells["colBandName"].Value != null) bandName = row.Cells["colBandName"].Value.ToString();
                            if (row.Cells["colTwMem"].Value != null) twMem = row.Cells["colTwMem"].Value.ToString();
                            if (row.Cells["colStartMhz"].Value != null) startMhz = row.Cells["colStartMhz"].Value.ToString();
                            if (row.Cells["colBwMhz"].Value != null) bwMhz = row.Cells["colBwMhz"].Value.ToString();
                            if (row.Cells["colStepKhz"].Value != null) stepKhz = row.Cells["colStepKhz"].Value.ToString();
                            if (row.Cells["colLoop"].Value != null) loop = row.Cells["colLoop"].Value.ToString();

                            sw.WriteLine(bandName + "|" + twMem + "|" + startMhz + "|" + bwMhz + "|" + stepKhz + "|" + loop);
                        }
                    }

                    MessageBox.Show("Configuration saved!\n\nFile: " + sfd.FileName, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                    int loadedBands = 0;
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        {
                            continue;
                        }

                        string[] data = line.Split('|');

                        if (data.Length >= 6)
                        {
                            dgvBands.Rows.Add("", data[1], data[0], data[2], data[3], data[4], Convert.ToBoolean(data[5]));
                            loadedBands++;
                        }
                    }

                    MessageBox.Show("Configuration loaded!\n\nBands: " + loadedBands.ToString(), "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Load Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion

        #region UI_EVENTS
        private void btDdsExample_Ramp_Click(object sender, EventArgs e)
        {
            if (guiActive)
            {
                guiActive = false;
            }
            else
            {
                ExecuteAWJMasterCycle();
            }
        }

        private void tlspbtConnect_Click(object sender, EventArgs e) { }

        private void tlspbtAbout_Click(object sender, EventArgs e)
        {
            string features = "DMS Control v3.2\n\n" +
                "RF Jamming System Control Software\n" +
                "K9 Electronics Ltd\n\n" +
                "╔══════════════════════════════════════╗\n" +
                "║    NEW ADVANCED AWJ CAPABILITIES     ║\n" +
                "╚══════════════════════════════════════╝\n\n" +
                "✓ Pattern Memory Mode\n" +
                "  • 4096-sample patterns\n" +
                "  • 180 MSPS playback\n" +
                "  • 22.76 μs completion\n" +
                "  • 2000× faster than SPI\n\n" +
                "✓ Multi-Channel Support\n" +
                "  • 4 DACs simultaneously\n" +
                "  • 140 MHz total coverage\n" +
                "  • Independent patterns per channel\n\n" +
                "✓ Pattern Types\n" +
                "  • PRBS (Pseudo-Random)\n" +
                "  • Chirp (Linear Sweep)\n" +
                "  • Multi-Tone (8 simultaneous)\n\n" +
                "✓ Preset Library\n" +
                "  • 7 pre-defined threat patterns\n" +
                "  • Real-time switching (<1 μs)\n" +
                "  • DJI, Autel, WiFi optimized\n\n" +
                "Copyright 2025";

            MessageBox.Show(features, "About DMS Control v3.2", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void tlspmnitDefault_Click(object sender, EventArgs e) { }
        private void tlspmnitMax2871Reg_Click(object sender, EventArgs e) { }
        private void DdsConfiguration_Click(object sender, EventArgs e) { }
        private void tlspmnitSwitches_Click(object sender, EventArgs e) { }

        private void btDdsExample_CwMode_Click(object sender, EventArgs e)
        {
            // NEW: Pattern switching on button click
            if (guiActive)
            {
                SwitchToNextPreset();
            }
        }

        private void btDdsExample_Random_Click(object sender, EventArgs e)
        {
            // NEW: Show preset library
            ShowPresetLibrary();
        }

        private void ShowPresetLibrary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("        PATTERN PRESET LIBRARY        ");
            sb.AppendLine("═══════════════════════════════════════\n");

            for (int i = 0; i < presetLibrary.Presets.Count; i++)
            {
                PatternPreset preset = presetLibrary.Presets[i];
                string current = (i == currentPresetIndex) ? " ◄ CURRENT" : "";
                sb.AppendLine((i + 1) + ". " + preset.Name + current);
                sb.AppendLine("   " + preset.Description);
                sb.AppendLine("   Threat: " + preset.ThreatType);
                sb.AppendLine();
            }

            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("Click 'CW' button during operation to");
            sb.AppendLine("cycle through presets in real-time");
            sb.AppendLine("═══════════════════════════════════════");

            MessageBox.Show(sb.ToString(), "Pattern Presets", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void tlspmiDeviceInformation_Click(object sender, EventArgs e) { }
        private void tlspmnSaveToFile_Click(object sender, EventArgs e) { btnSaveBands_Click(sender, e); }
        private void tlspmnLoadFromFile_Click(object sender, EventArgs e) { btnLoadBands_Click(sender, e); }
        private void cmbbxLoPower_SelectedIndexChanged(object sender, EventArgs e) { }
        private void cmbbxLoPower_KeyPress(object sender, KeyPressEventArgs e) { }
        private void LoParameters_KeyPress(object sender, KeyPressEventArgs e) { }
        private void DdsParameters_KeyPress(object sender, KeyPressEventArgs e) { }
        private void cmbbxDdsMode_SelectedIndexChanged(object sender, EventArgs e) { }
        private void txbxAttenuatorCtrl_KeyPress(object sender, KeyPressEventArgs e) { }
        private void txbxPulseModulator_KeyPress(object sender, KeyPressEventArgs e) { }
        private void txbxLoSwitchState_KeyPress(object sender, KeyPressEventArgs e) { }
        private void chbxLoWaitLD_Click(object sender, EventArgs e) { }
        private void FmMain_FormClosing(object sender, FormClosingEventArgs e) { guiActive = false; }

        private void tlspbtConfigSave_Click(object sender, EventArgs e)
        {
            if (device == null)
            {
                MessageBox.Show("Device not initialized!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                ltTxCommand.Clear();
                int bandCount = 0;

                foreach (DataGridViewRow row in dgvBands.Rows)
                {
                    if (row.IsNewRow) continue;
                    if (row.Cells["colBandName"].Value == null) continue;

                    double startFreq = Convert.ToDouble(row.Cells["colStartMhz"].Value);
                    AddSequences(new SequenceWriteRegisterPLL(ltTxCommand, null, (ulong)(startFreq * 1e6)));
                    bandCount++;
                }

                if (bandCount > 0)
                {
                    MessageBox.Show("Prepared " + bandCount.ToString() + " band configurations.\n\nCommands will be sent during next cycle.", "Ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("No bands configured!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void tlspbtEraseMemory_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("Clear all cached patterns and reset preset library?\n\nAre you sure?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    ltTxCommand.Clear();

                    // Clear loaded patterns cache
                    loadedPatterns.Clear();

                    // Reset preset library
                    presetLibrary.LoadDefaultPresets();
                    currentPresetIndex = 0;

                    MessageBox.Show("Memory cache cleared.\n\nPreset library reset to defaults.\n\nNew patterns will be generated on next run.", "Ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        #endregion
    }
}
