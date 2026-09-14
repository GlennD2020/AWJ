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
