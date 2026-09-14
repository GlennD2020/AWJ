<#
.SYNOPSIS
    K9 Electronics - FmMain.cs Conflict Resolution Patcher
.DESCRIPTION
    Resolves:
      1. Centre Freq (LO) vs Table Start MHz conflict
      2. Left-side DDS params vs Table params conflict  
      3. Duplicate BW column (colBwMhz vs colBandwidthMhz)
.USAGE
    .\Apply-Patches.ps1 -InputFile "FmMain.cs"
    Output: FmMain_patched.cs (same folder)
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$InputFile
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $InputFile)) {
    Write-Host "ERROR: File not found: $InputFile" -ForegroundColor Red
    exit 1
}

$OutputFile = $InputFile -replace '\.cs$', '_patched.cs'
$source = [System.IO.File]::ReadAllText($InputFile, [System.Text.Encoding]::UTF8)
$originalLen = $source.Length
$patchCount = 0
$totalPatches = 12

Write-Host ""
Write-Host "K9 FmMain.cs Conflict Resolution Patcher" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Input:  $InputFile"
Write-Host "Output: $OutputFile"
Write-Host "Original size: $originalLen chars"
Write-Host ""

function Apply-Patch {
    param([ref]$Source, [string]$Old, [string]$New, [string]$Label)
    
    if ($Source.Value.Contains($Old)) {
        $Source.Value = $Source.Value.Replace($Old, $New)
        Write-Host "  OK: [$Label]" -ForegroundColor Green
        return $true
    } else {
        Write-Host "  WARNING: Could not find anchor for [$Label]" -ForegroundColor Yellow
        return $false
    }
}

# ═══════════════════════════════════════════════════════════════
# PATCH 1: Add saved-state fields for jamming save/restore
# ═══════════════════════════════════════════════════════════════
Write-Host "PATCH 1: Add saved hardware state fields..."

$old1 = @"
        #endregion

        #endregion  //  PARAMETERS
"@

$new1 = @"
        #endregion

        #region JAMMING_STATE_FIELDS
        /// <summary> Saved LO state - restored when jamming stops. </summary>
        private double savedLoFrequency = 0;
        private float savedLoStep = 0;
        private uint savedLoPoints = 0;
        private float savedLoHoldTime = 0;
        private int savedLoWaitLD = 0;
        private int savedLoSweepOn = 0;
        /// <summary> Saved DDS state - restored when jamming stops. </summary>
        private ushort savedDdsSweepOn = 0;
        private double savedDdsStart = 0;
        private double savedDdsStep = 0;
        private ulong savedDdsPoints = 0;
        private float savedDdsFreqCtrl = 0;
        private ushort savedDdsTwMem = 0;
        /// <summary> Tracks whether left-side controls are locked during jamming. </summary>
        private bool controlsLockedForJamming = false;
        #endregion

        #endregion  //  PARAMETERS
"@

if (Apply-Patch ([ref]$source) $old1 $new1 "Saved state fields") { $patchCount++ }


# ═══════════════════════════════════════════════════════════════
# PATCH 2: Hide duplicate BW column in EnsureGridColumns
# ═══════════════════════════════════════════════════════════════
Write-Host "PATCH 2: Hide duplicate BW column..."

$old2 = @"
        private void EnsureGridColumns()
        {
            if (dgvBands == null) return;
"@

$new2 = @"
        private void EnsureGridColumns()
        {
            if (dgvBands == null) return;

            // K9 FIX: Hide Designer's duplicate BW column.
            // Only colBandwidthMhz (added at runtime below) is read by jamming logic.
            // Also hide Step KHz - jamming calculates step from bandwidth/points.
            if (dgvBands.Columns.Contains("colBwMhz"))
                dgvBands.Columns["colBwMhz"].Visible = false;
            if (dgvBands.Columns.Contains("colStepKhz"))
                dgvBands.Columns["colStepKhz"].Visible = false;
"@

if (Apply-Patch ([ref]$source) $old2 $new2 "Hide duplicate columns") { $patchCount++ }


# ═══════════════════════════════════════════════════════════════
# PATCH 3: Add save/restore/lock/unlock methods
# ═══════════════════════════════════════════════════════════════
Write-Host "PATCH 3: Add save/restore/lock/unlock methods..."

$old3 = @"
        #region GUARD_BAND_JAMMING

        /// <summary>
        /// Start Guard Band Jamming
        /// </summary>
"@

$new3 = @"
        #region JAMMING_STATE_MANAGEMENT

        /// <summary>
        /// Saves current LO and DDS hardware state so it can be restored
        /// after jamming stops. Call on UI thread before jamming begins.
        /// </summary>
        private void SaveHardwareState()
        {
            savedLoFrequency = device.lo.pll.OutFrequency;
            savedLoStep = device.lo.Step;
            savedLoPoints = device.lo.Points;
            savedLoHoldTime = device.lo.HoldTime;
            savedLoWaitLD = device.lo.WaitLD;
            savedLoSweepOn = device.lo.SweepOn;

            savedDdsSweepOn = device.dds.SweepOn;
            savedDdsStart = device.dds.Start;
            savedDdsStep = device.dds.Step;
            savedDdsPoints = device.dds.Points;
            savedDdsFreqCtrl = device.dds.FreqCtrl;
            savedDdsTwMem = device.dds.registers[0x47];

            Debug.WriteLine("| Hardware state saved: LO=" +
                (savedLoFrequency / 1e6).ToString("F1") + " MHz, DDS mode=" + savedDdsSweepOn);
        }

        /// <summary>
        /// Restores LO and DDS to their pre-jamming state and sends
        /// the configuration back to the hardware. Call on UI thread.
        /// </summary>
        private void RestoreHardwareState()
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
            device.dds.registers[0x47] = savedDdsTwMem;

            AddSequences(new SequenceSetSweepLoParameters(ltTxCommand, null,
                (float)savedLoFrequency, savedLoStep, savedLoPoints,
                savedLoHoldTime, savedLoWaitLD, savedLoSweepOn));

            DDSSetConfiguration();

            UpdateMainForm(device);
            PrepareGroupBoxDDS(device.dds.SweepOn);

            if (cmbbxDdsMode != null && savedDdsSweepOn < cmbbxDdsMode.Items.Count)
            {
                k9ExtendedMode = savedDdsSweepOn;
                cmbbxDdsMode.SelectedIndex = savedDdsSweepOn;
            }

            Debug.WriteLine("| Hardware state restored: LO=" +
                (savedLoFrequency / 1e6).ToString("F1") + " MHz, DDS mode=" + savedDdsSweepOn);
        }

        /// <summary>
        /// Locks left-side LO and DDS controls during jamming.
        /// Fields become read-only indicators showing live hop state.
        /// </summary>
        private void SetControlsJammingMode(bool locked)
        {
            controlsLockedForJamming = locked;
            Color lockColour = locked ? Color.FromArgb(255, 255, 220) : SystemColors.Window;
            bool editable = !locked;

            if (txbxLoStart != null) { txbxLoStart.ReadOnly = locked; txbxLoStart.BackColor = lockColour; }
            if (txbxLoStep != null) { txbxLoStep.ReadOnly = locked; txbxLoStep.BackColor = lockColour; }
            if (txbxLoPoints != null) { txbxLoPoints.ReadOnly = locked; txbxLoPoints.BackColor = lockColour; }
            if (txbxLoDelays != null) { txbxLoDelays.ReadOnly = locked; txbxLoDelays.BackColor = lockColour; }
            if (cmbbxLoPower != null) cmbbxLoPower.Enabled = editable;

            if (txbxDdsStart != null) { txbxDdsStart.ReadOnly = locked; txbxDdsStart.BackColor = lockColour; }
            if (txbxDdsStep != null) { txbxDdsStep.ReadOnly = locked; txbxDdsStep.BackColor = lockColour; }
            if (txbxDdsPoints != null) { txbxDdsPoints.ReadOnly = locked; txbxDdsPoints.BackColor = lockColour; }
            if (txbxDdsBandwith != null) { txbxDdsBandwith.ReadOnly = locked; txbxDdsBandwith.BackColor = lockColour; }
            if (txbxDdsFreqCtrl != null) { txbxDdsFreqCtrl.ReadOnly = locked; txbxDdsFreqCtrl.BackColor = lockColour; }
            if (txbxDdsTwMem != null) { txbxDdsTwMem.ReadOnly = locked; txbxDdsTwMem.BackColor = lockColour; }
            if (cmbbxDdsMode != null) cmbbxDdsMode.Enabled = editable;

            if (locked)
            {
                string title = this.Text;
                if (!title.Contains("[JAMMING]"))
                    this.Text = title + "  [JAMMING]";
            }
            else
            {
                this.Text = this.Text.Replace("  [JAMMING]", "");
            }
        }

        /// <summary>
        /// Updates left-side LO display to show the current hop frequency.
        /// Called from background thread via Invoke during TDM jamming.
        /// </summary>
        private void UpdateLiveLoDisplay(double frequencyMHz)
        {
            if (!controlsLockedForJamming) return;
            try
            {
                this.Invoke((MethodInvoker)delegate
                {
                    if (txbxLoStart != null)
                        txbxLoStart.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(
                            frequencyMHz * 1e6, 3);
                });
            }
            catch { }
        }

        /// <summary>
        /// Updates left-side DDS display to show the current jamming DDS config.
        /// Called once when DDS is configured for jamming.
        /// </summary>
        private void UpdateLiveDdsDisplay(double bandwidthMHz, uint points, float freqCtrl)
        {
            if (!controlsLockedForJamming) return;
            try
            {
                this.Invoke((MethodInvoker)delegate
                {
                    if (txbxDdsBandwith != null)
                        txbxDdsBandwith.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(
                            bandwidthMHz * 1e6, 3);
                    if (txbxDdsPoints != null)
                        txbxDdsPoints.Text = points.ToString();
                    if (txbxDdsFreqCtrl != null)
                        txbxDdsFreqCtrl.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue(
                            freqCtrl, 3);
                });
            }
            catch { }
        }

        #endregion  // JAMMING_STATE_MANAGEMENT

        #region GUARD_BAND_JAMMING

        /// <summary>
        /// Start Guard Band Jamming
        /// </summary>
"@

if (Apply-Patch ([ref]$source) $old3 $new3 "Save/restore/lock methods") { $patchCount++ }


# ═══════════════════════════════════════════════════════════════
# PATCH 4: START button saves state + locks controls
# ═══════════════════════════════════════════════════════════════
Write-Host "PATCH 4: START button saves state..."

$old4 = @"
            btnGuardStart.Enabled = false;
            btnGuardStop.Enabled = true;
            isJamming = true;

            Thread jammingThread = new Thread(ExecuteGuardBandJamming);
"@

$new4 = @"
            SaveHardwareState();
            SetControlsJammingMode(true);

            btnGuardStart.Enabled = false;
            btnGuardStop.Enabled = true;
            isJamming = true;

            Thread jammingThread = new Thread(ExecuteGuardBandJamming);
"@

if (Apply-Patch ([ref]$source) $old4 $new4 "START saves state") { $patchCount++ }


# ═══════════════════════════════════════════════════════════════
# PATCH 5: STOP button restores state + unlocks controls
# ═══════════════════════════════════════════════════════════════
Write-Host "PATCH 5: STOP button restores state..."

$old5 = @"
        private void btnGuardStop_Click(object sender, EventArgs e)
        {
            isJamming = false;

            // Stop DDS output immediately
            AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0));

            if (btnGuardStart != null) btnGuardStart.Enabled = true;
            if (btnGuardStop != null) btnGuardStop.Enabled = false;
            UpdateGuardStatus();
            Debug.WriteLine("║ GUARD BAND JAMMING STOPPED BY USER ║");
        }
"@

$new5 = @"
        private void btnGuardStop_Click(object sender, EventArgs e)
        {
            isJamming = false;

            // Stop DDS output immediately
            AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0));

            // Restore pre-jamming LO and DDS state
            RestoreHardwareState();
            SetControlsJammingMode(false);

            if (btnGuardStart != null) btnGuardStart.Enabled = true;
            if (btnGuardStop != null) btnGuardStop.Enabled = false;
            UpdateGuardStatus();
            Debug.WriteLine("| GUARD BAND JAMMING STOPPED - STATE RESTORED |");
        }
"@

if (Apply-Patch ([ref]$source) $old5 $new5 "STOP restores state") { $patchCount++ }


# ═══════════════════════════════════════════════════════════════
# PATCH 6: HopToFrequency updates live LO display
# ═══════════════════════════════════════════════════════════════
Write-Host "PATCH 6: HopToFrequency updates live LO display..."

$old6 = @"
                // Fast PLL update - single sequence command
                AddSequences(new SequenceSetSweepLoParameters(ltTxCommand, null,
                    (float)freqHz, 0, 1, 0, 0, 0));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("│ ✗ Hop error: " + ex.Message);
            }
        }
"@

$new6 = @"
                // Fast PLL update - single sequence command
                AddSequences(new SequenceSetSweepLoParameters(ltTxCommand, null,
                    (float)freqHz, 0, 1, 0, 0, 0));

                // Update left-side LO display to show current hop frequency
                UpdateLiveLoDisplay(frequencyMHz);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("| Hop error: " + ex.Message);
            }
        }
"@

if (Apply-Patch ([ref]$source) $old6 $new6 "Live LO display during hops") { $patchCount++ }


# ═══════════════════════════════════════════════════════════════
# PATCH 7: ConfigureDDSForJamming updates live DDS display
# ═══════════════════════════════════════════════════════════════
Write-Host "PATCH 7: ConfigureDDSForJamming updates live DDS display..."

$old7 = @"
                // Wait for DDS to stabilize
                Thread.Sleep(300);

                Debug.WriteLine("│ DDS configured: RAMP mode, BW=" + firstBand.BandwidthMHz + " MHz, Points=" + points);
"@

$new7 = @"
                // Wait for DDS to stabilize
                Thread.Sleep(300);

                // Update left-side DDS fields to show jamming config
                UpdateLiveDdsDisplay(firstBand.BandwidthMHz, points, device.dds.FreqCtrl);

                Debug.WriteLine("| DDS configured: RAMP mode, BW=" + firstBand.BandwidthMHz + " MHz, Points=" + points);
"@

if (Apply-Patch ([ref]$source) $old7 $new7 "Live DDS display during jamming") { $patchCount++ }


# ═══════════════════════════════════════════════════════════════
# PATCH 8: Jamming thread restores state on completion/error
# ═══════════════════════════════════════════════════════════════
Write-Host "PATCH 8: Restore state on jamming completion/error..."

$old8 = @"
                this.Invoke((MethodInvoker)delegate
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
                });
            }
            finally
            {
                isJamming = false;
            }
"@

$new8 = @"
                this.Invoke((MethodInvoker)delegate
                {
                    RestoreHardwareState();
                    SetControlsJammingMode(false);

                    if (btnGuardStart != null) btnGuardStart.Enabled = true;
                    if (btnGuardStop != null) btnGuardStop.Enabled = false;
                    UpdateGuardStatus();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("| ERROR: " + ex.Message);
                this.Invoke((MethodInvoker)delegate
                {
                    try { RestoreHardwareState(); } catch { }
                    try { SetControlsJammingMode(false); } catch { }

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
"@

if (Apply-Patch ([ref]$source) $old8 $new8 "Restore on completion/error") { $patchCount++ }


# ═══════════════════════════════════════════════════════════════
# PATCH 9: Block LO edits during jamming
# ═══════════════════════════════════════════════════════════════
Write-Host "PATCH 9: Block LO edits during jamming..."

$old9 = @"
        private void LoParameters_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                String tag = (String)(((TextBox)sender).Tag);
                if (e.KeyChar == 13)
                {
"@

$new9 = @"
        private void LoParameters_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                if (controlsLockedForJamming) { e.Handled = true; return; }

                String tag = (String)(((TextBox)sender).Tag);
                if (e.KeyChar == 13)
                {
"@

if (Apply-Patch ([ref]$source) $old9 $new9 "Block LO edits during jamming") { $patchCount++ }


# ═══════════════════════════════════════════════════════════════
# PATCH 10: Block DDS edits during jamming
# ═══════════════════════════════════════════════════════════════
Write-Host "PATCH 10: Block DDS edits during jamming..."

$old10 = @"
        private void DdsParameters_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                String tag = (String)(((TextBox)sender).Tag);

                if (e.KeyChar == 13)
                {
"@

$new10 = @"
        private void DdsParameters_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                if (controlsLockedForJamming) { e.Handled = true; return; }

                String tag = (String)(((TextBox)sender).Tag);

                if (e.KeyChar == 13)
                {
"@

if (Apply-Patch ([ref]$source) $old10 $new10 "Block DDS edits during jamming") { $patchCount++ }


# ═══════════════════════════════════════════════════════════════
# PATCH 11: Block mode changes during jamming
# ═══════════════════════════════════════════════════════════════
Write-Host "PATCH 11: Block mode changes during jamming..."

$old11 = @"
        private void cmbbxDdsMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                int selectedIdx = cmbbxDdsMode.SelectedIndex;
"@

$new11 = @"
        private void cmbbxDdsMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (controlsLockedForJamming) return;

                int selectedIdx = cmbbxDdsMode.SelectedIndex;
"@

if (Apply-Patch ([ref]$source) $old11 $new11 "Block mode change during jamming") { $patchCount++ }


# ═══════════════════════════════════════════════════════════════
# PATCH 12: Block LO power changes during jamming
# ═══════════════════════════════════════════════════════════════
Write-Host "PATCH 12: Block LO power changes during jamming..."

$old12 = @"
        private void cmbbxLoPower_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (guiActive == true)
                {
"@

$new12 = @"
        private void cmbbxLoPower_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (controlsLockedForJamming) return;

                if (guiActive == true)
                {
"@

if (Apply-Patch ([ref]$source) $old12 $new12 "Block LO power change during jamming") { $patchCount++ }


# ═══════════════════════════════════════════════════════════════
# WRITE OUTPUT
# ═══════════════════════════════════════════════════════════════
[System.IO.File]::WriteAllText($OutputFile, $source, [System.Text.Encoding]::UTF8)

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Patches applied: $patchCount / $totalPatches" -ForegroundColor $(if ($patchCount -eq $totalPatches) { "Green" } else { "Yellow" })
Write-Host "Output written:  $OutputFile"
Write-Host "Output size:     $($source.Length) chars (+$($source.Length - $originalLen))"
Write-Host "=========================================" -ForegroundColor Cyan

if ($patchCount -lt $totalPatches) {
    Write-Host ""
    Write-Host "WARNING: $($totalPatches - $patchCount) patch(es) could not be applied." -ForegroundColor Yellow
    Write-Host "Check if FmMain.cs has already been modified." -ForegroundColor Yellow
} else {
    Write-Host ""
    Write-Host "All patches applied successfully." -ForegroundColor Green
    Write-Host ""
    Write-Host "Changes:" -ForegroundColor White
    Write-Host "  * Saved-state fields added for LO + DDS save/restore"
    Write-Host "  * Duplicate BW column (colBwMhz) hidden"
    Write-Host "  * Left-side controls locked during jamming (yellow background)"
    Write-Host "  * LO display updates live during frequency hops"
    Write-Host "  * DDS display updates when jamming configures waveform"
    Write-Host "  * Hardware state fully restored when jamming stops"
    Write-Host "  * All left-side input handlers guarded against edits"
    Write-Host "  * Title bar shows [JAMMING] during operation"
}
