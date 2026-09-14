#!/usr/bin/env python3
"""
K9 Electronics - DMS FmMain.cs Patcher
Applies all patches for parameter conflict resolution + DDS per-band fix + Thread.Sleep fix
"""
import sys, os

def patch(source, anchor, replacement, mode='replace', label=''):
    if anchor not in source:
        print(f"  WARNING: Anchor not found for [{label}]")
        return source
    if mode == 'replace':
        result = source.replace(anchor, replacement, 1)
    elif mode == 'after':
        result = source.replace(anchor, anchor + replacement, 1)
    elif mode == 'before':
        result = source.replace(anchor, replacement + anchor, 1)
    else:
        result = source
    if result == source:
        print(f"  WARNING: No change for [{label}]")
    else:
        print(f"  OK: [{label}]")
    return result

def main():
    input_path = sys.argv[1] if len(sys.argv) > 1 else 'FmMain.cs'
    output_path = sys.argv[2] if len(sys.argv) > 2 else 'FmMain_patched_final.cs'

    print(f"Reading {input_path}...")
    with open(input_path, 'r', encoding='utf-8-sig') as f:
        src = f.read()
    original_len = len(src)

    print("\n=== PATCH 1: Thread.Sleep fix ===")
    count = src.count('Thread.Sleep(new TimeSpan(10))')
    src = src.replace('Thread.Sleep(new TimeSpan(10))', 'Thread.Sleep(10)')
    print(f"  OK: Replaced {count} instances of Thread.Sleep(new TimeSpan(10))")

    print("\n=== PATCH 2: Add saved state fields ===")
    src = patch(src,
        'private volatile bool isJamming = false;',
        '''private volatile bool isJamming = false;

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
        #endregion''',
        mode='replace', label='JAMMING_STATE_FIELDS')

    print("\n=== PATCH 3: Add state management methods ===")
    src = patch(src,
        '#region GUARD_BAND_JAMMING',
        r'''#region JAMMING_STATE_MANAGEMENT

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

        #region GUARD_BAND_JAMMING''',
        mode='replace', label='STATE_MANAGEMENT_METHODS')

    print("\n=== PATCH 4: btnGuardStart - save state ===")
    src = patch(src,
        '''btnGuardStart.Enabled = false;
            btnGuardStop.Enabled = true;
            isJamming = true;''',
        '''btnGuardStart.Enabled = false;
            btnGuardStop.Enabled = true;
            isJamming = true;

            // Save current hardware state and lock controls
            SaveHardwareState();
            SetControlsJammingMode(true);''',
        mode='replace', label='btnGuardStart_SaveState')

    print("\n=== PATCH 5: btnGuardStop - restore state ===")
    src = patch(src,
        '''isJamming = false;

            // Stop DDS output immediately
            AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0));

            if (btnGuardStart != null) btnGuardStart.Enabled = true;
            if (btnGuardStop != null) btnGuardStop.Enabled = false;
            UpdateGuardStatus();''',
        '''isJamming = false;

            // Stop DDS output immediately
            AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0));

            // Restore hardware state and unlock controls
            RestoreHardwareState();
            SetControlsJammingMode(false);

            if (btnGuardStart != null) btnGuardStart.Enabled = true;
            if (btnGuardStop != null) btnGuardStop.Enabled = false;
            UpdateGuardStatus();''',
        mode='replace', label='btnGuardStop_RestoreState')

    print("\n=== PATCH 6: HopToFrequency - live display ===")
    src = patch(src,
        '''// Fast PLL update - single sequence command
                AddSequences(new SequenceSetSweepLoParameters(ltTxCommand, null,
                    (float)freqHz, 0, 1, 0, 0, 0));''',
        '''// Fast PLL update - single sequence command
                AddSequences(new SequenceSetSweepLoParameters(ltTxCommand, null,
                    (float)freqHz, 0, 1, 0, 0, 0));

                // Update live LO display
                UpdateLiveLoDisplay(frequencyMHz);''',
        mode='replace', label='HopToFrequency_LiveDisplay')

    print("\n=== PATCH 7: LoParameters_KeyPress guard ===")
    src = patch(src,
        '''private void LoParameters_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                String tag = (String)(((TextBox)sender).Tag);
                if (e.KeyChar == 13)''',
        '''private void LoParameters_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                if (isJamming) { e.Handled = true; return; }

                String tag = (String)(((TextBox)sender).Tag);
                if (e.KeyChar == 13)''',
        mode='replace', label='LoParameters_Guard')

    print("\n=== PATCH 8: DdsParameters_KeyPress guard ===")
    src = patch(src,
        '''private void DdsParameters_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                String tag = (String)(((TextBox)sender).Tag);

                if (e.KeyChar == 13)''',
        '''private void DdsParameters_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                if (isJamming) { e.Handled = true; return; }

                String tag = (String)(((TextBox)sender).Tag);

                if (e.KeyChar == 13)''',
        mode='replace', label='DdsParameters_Guard')

    print("\n=== PATCH 9: cmbbxDdsMode guard ===")
    src = patch(src,
        '''private void cmbbxDdsMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                int selectedIdx = cmbbxDdsMode.SelectedIndex;''',
        '''private void cmbbxDdsMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (isJamming) return;

                int selectedIdx = cmbbxDdsMode.SelectedIndex;''',
        mode='replace', label='cmbbxDdsMode_Guard')

    print("\n=== PATCH 10: cmbbxLoPower guard ===")
    src = patch(src,
        '''private void cmbbxLoPower_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (guiActive == true)''',
        '''private void cmbbxLoPower_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (isJamming) return;

                if (guiActive == true)''',
        mode='replace', label='cmbbxLoPower_Guard')

    print("\n=== PATCH 11: Tones multiplier 256->32 ===")
    src = patch(src,
        'Math.Max(firstBand.Tones * 16, 256)',
        'Math.Max(firstBand.Tones * 16, 32)',
        mode='replace', label='Tones_256to32')

    print("\n=== PATCH 12: ConfigureDDS live display ===")
    src = patch(src,
        'Debug.WriteLine("│ DDS configured: RAMP mode, BW=" + firstBand.BandwidthMHz + " MHz, Points=" + points);',
        '''// Update live DDS display
                UpdateLiveDdsDisplay(firstBand.BandwidthMHz, points, device.dds.FreqCtrl);

                Debug.WriteLine("│ DDS configured: RAMP mode, BW=" + firstBand.BandwidthMHz + " MHz, Points=" + points);''',
        mode='replace', label='ConfigureDDS_LiveDisplay')

    print("\n=== PATCH 13: Pulsed TDM per-band reconfig ===")
    src = patch(src,
        '''int timePerBand = Math.Max(pulseOnTimeMs / allBands.Count, 2);

                    for (int cycle = 0; cycle < pulseCycles && isJamming; cycle++)
                    {
                        // ── JAM PHASE: hop through all bands ──
                        for (int i = 0; i < allBands.Count && isJamming; i++)
                        {
                            HopToFrequency(allBands[i].FrequencyMHz);''',
        '''int timePerBand = Math.Max(pulseOnTimeMs / allBands.Count, 2);
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
                            HopToFrequency(allBands[i].FrequencyMHz);''',
        mode='replace', label='PulsedTDM_PerBand')

    print("\n=== PATCH 14: Continuous TDM per-band reconfig ===")
    src = patch(src,
        '''int dwellMs = Math.Max(5, 50 / allBands.Count);

                    while (isJamming)
                    {
                        for (int i = 0; i < allBands.Count && isJamming; i++)
                        {
                            HopToFrequency(allBands[i].FrequencyMHz);''',
        '''int dwellMs = Math.Max(5, 50 / allBands.Count);
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
                            HopToFrequency(allBands[i].FrequencyMHz);''',
        mode='replace', label='ContinuousTDM_PerBand')

    print("\n=== PATCH 15: Restore state on jamming complete ===")
    src = patch(src,
        '''// ── Stop output ──
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0));
                Debug.WriteLine("│ ✓ Jamming complete");''',
        '''// ── Stop output and restore state ──
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0));
                RestoreHardwareState();
                Debug.WriteLine("│ ✓ Jamming complete - state restored");''',
        mode='replace', label='Jamming_RestoreOnComplete')

    src = patch(src,
        '''this.Invoke((MethodInvoker)delegate
                {
                    if (btnGuardStart != null) btnGuardStart.Enabled = true;
                    if (btnGuardStop != null) btnGuardStop.Enabled = false;
                    UpdateGuardStatus();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("│ ✗ ERROR: " + ex.Message);
                this.Invoke((MethodInvoker)delegate
                {
                    MessageBox.Show("Guard Band Error: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    if (btnGuardStart != null) btnGuardStart.Enabled = true;
                    if (btnGuardStop != null) btnGuardStop.Enabled = false;
                });''',
        '''this.Invoke((MethodInvoker)delegate
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
                });''',
        mode='replace', label='Jamming_RestoreOnError')

    print(f"\n{'='*60}")
    print(f"Original: {original_len} chars")
    print(f"Patched:  {len(src)} chars")
    print(f"Added:    +{len(src) - original_len} chars")
    print(f"Output:   {output_path}")
    print(f"{'='*60}")

    with open(output_path, 'w', encoding='utf-8') as f:
        f.write(src)
    print("\nDone!")

if __name__ == '__main__':
    main()
