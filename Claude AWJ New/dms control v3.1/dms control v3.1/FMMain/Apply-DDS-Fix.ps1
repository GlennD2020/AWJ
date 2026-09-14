<#
.SYNOPSIS
    K9 Electronics - Per-Band DDS Fix
.DESCRIPTION
    Fixes:
      1. DDS now reconfigures per-band (BW + Tones from each row)
      2. Tones multiplier lowered so values 4-64 all produce different results
      3. Live DDS display updates per-hop
.USAGE
    .\Apply-DDS-Fix.ps1 -InputFile "FmMain_patched.cs"
    Output: FmMain_patched2.cs
.NOTES
    Apply AFTER Apply-Patches.ps1
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

$OutputFile = $InputFile -replace '\.cs$', '_ddsfix.cs'
$source = [System.IO.File]::ReadAllText($InputFile, [System.Text.Encoding]::UTF8)
$originalLen = $source.Length
$patchCount = 0
$totalPatches = 4

Write-Host ""
Write-Host "K9 Per-Band DDS Fix" -ForegroundColor Cyan
Write-Host "===================" -ForegroundColor Cyan
Write-Host "Input:  $InputFile"
Write-Host "Output: $OutputFile"
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
# PATCH A: Replace ConfigureDDSForJamming — one-time register
#          setup only, no longer sets sweep params (those move
#          to per-band ReconfigureDDSBandParams)
# ═══════════════════════════════════════════════════════════════
Write-Host "PATCH A: Split DDS config into one-time + per-band..."

$oldA = @"
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
                uint points = (uint)Math.Max(firstBand.Tones * 16, 256);
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

                // Update left-side DDS fields to show jamming config
                UpdateLiveDdsDisplay(firstBand.BandwidthMHz, points, device.dds.FreqCtrl);

                Debug.WriteLine("│ DDS configured: RAMP mode, BW=" + firstBand.BandwidthMHz + " MHz, Points=" + points);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("│ ✗ DDS config error: " + ex.Message);
            }
        }
"@

$newA = @"
        /// <summary>
        /// One-time AD9106 register setup for jamming.
        /// Configures RAMP mode channel routing, SAW config, and pattern memory.
        /// Does NOT set sweep parameters — those are set per-band by ReconfigureDDSBandParams.
        /// </summary>
        private void ConfigureDDSForJamming(BandConfiguration firstBand)
        {
            try
            {
                ushort ddsMode = (ushort)AD9106_MODE.RAMP;
                device.dds.SweepOn = ddsMode;

                // Calculate initial sweep params from first band (will be overwritten per-band)
                uint points = (uint)Math.Max(firstBand.Tones * 16, 32);
                double bwHz = firstBand.BandwidthMHz * 1e6;

                // Configure AD9106 registers for RAMP mode — channel routing and pattern memory
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

                // Wait for registers to settle
                Thread.Sleep(200);

                // Now apply first band's sweep params (same path as per-band updates)
                ReconfigureDDSBandParams(firstBand);

                // Start DDS output
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 10));
                AddSequences(new SequenceDelay(200));
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0));

                // Wait for DDS to stabilize
                Thread.Sleep(200);

                Debug.WriteLine("| DDS one-time setup complete, first band: BW=" + firstBand.BandwidthMHz + " MHz, Tones=" + firstBand.Tones);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("| DDS config error: " + ex.Message);
            }
        }

        /// <summary>
        /// Lightweight per-band DDS reconfiguration.
        /// Only updates sweep parameters (start/step/points) and FreqCtrl — does NOT
        /// re-write the full AD9106 register set. Fast enough for TDM band switching.
        ///
        /// Points = Tones x 16 (min 32). This controls spectral density:
        ///   Tones=4  -> 64 points  -> visible comb teeth on SA (~BW/4 spacing)
        ///   Tones=8  -> 128 points -> coarse fill
        ///   Tones=16 -> 256 points -> smooth fill
        ///   Tones=32 -> 512 points -> very smooth
        ///   Tones=64 -> 1024 points -> maximum density
        /// </summary>
        private void ReconfigureDDSBandParams(BandConfiguration band)
        {
            try
            {
                ushort ddsMode = (ushort)AD9106_MODE.RAMP;
                uint points = (uint)Math.Max(band.Tones * 16, 32);
                double bwHz = band.BandwidthMHz * 1e6;
                ulong ddsStart = (ulong)(bwHz / 2);
                ulong ddsStep = (ulong)(bwHz / points);
                float freqCtrl = (float)(bwHz / 10);

                device.dds.SweepOn = ddsMode;
                device.dds.Start = ddsStart;
                device.dds.Step = ddsStep;
                device.dds.Points = points;
                device.dds.FreqCtrl = freqCtrl;

                // Send sweep params — single USB command, fast
                AddSequences(new SequenceSetSweepDDS(ltTxCommand, null,
                    ddsMode, ddsStart, ddsStep, points));

                // Update SRAM address range registers for new point count
                for (int i = 0; i < 4; i++)
                {
                    device.dds.registers[(ushort)(0x52 + 4 * i)] = (ushort)(points * 16);
                    AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null,
                        (ushort)(0x52 + 4 * i), (ushort)(points * 16)));
                }

                // Latch the SRAM register changes
                AddSequences(new SequenceSetRegisterDDS(ltTxCommand, null, 0x1D, 0x0001));

                // Set sweep rate
                AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, freqCtrl));

                // Update live display
                UpdateLiveDdsDisplay(band.BandwidthMHz, points, freqCtrl);

                Debug.WriteLine("|   DDS reconfig: " + band.Name + " BW=" + band.BandwidthMHz + " MHz, Tones=" + band.Tones + " -> " + points + " pts");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("| DDS reconfig error: " + ex.Message);
            }
        }
"@

if (Apply-Patch ([ref]$source) $oldA $newA "Split DDS into one-time + per-band") { $patchCount++ }


# ═══════════════════════════════════════════════════════════════
# PATCH B: Modify PULSED TDM loop — reconfigure DDS per band
# ═══════════════════════════════════════════════════════════════
Write-Host "PATCH B: Pulsed TDM loop reconfigures DDS per band..."

$oldB = @"
                    Debug.WriteLine("│ Mode: PULSED TDM  ON=" + pulseOnTimeMs + "ms OFF=" + pulseOffTimeMs + "ms");
                    int timePerBand = Math.Max(pulseOnTimeMs / allBands.Count, 2);

                    for (int cycle = 0; cycle < pulseCycles && isJamming; cycle++)
                    {
                        // ── JAM PHASE: hop through all bands ──
                        for (int i = 0; i < allBands.Count && isJamming; i++)
                        {
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
"@

$newB = @"
                    Debug.WriteLine("| Mode: PULSED TDM  ON=" + pulseOnTimeMs + "ms OFF=" + pulseOffTimeMs + "ms");

                    // Allow extra dwell time per band to account for DDS reconfiguration
                    // when BW or Tones differ between bands
                    int baseDwellMs = Math.Max(pulseOnTimeMs / allBands.Count, 2);

                    // Track current DDS config to skip reconfigure when BW+Tones haven't changed
                    double currentBW = -1;
                    int currentTones = -1;

                    for (int cycle = 0; cycle < pulseCycles && isJamming; cycle++)
                    {
                        // ── JAM PHASE: hop through all bands ──
                        for (int i = 0; i < allBands.Count && isJamming; i++)
                        {
                            // Reconfigure DDS sweep only if this band's BW or Tones differ
                            if (allBands[i].BandwidthMHz != currentBW || allBands[i].Tones != currentTones)
                            {
                                ReconfigureDDSBandParams(allBands[i]);
                                currentBW = allBands[i].BandwidthMHz;
                                currentTones = allBands[i].Tones;
                                // Small settle time after DDS reconfig
                                Thread.Sleep(2);
                            }

                            HopToFrequency(allBands[i].FrequencyMHz);
                            UpdateTDMStatus(cycle, i, allBands.Count, true);
                            Thread.Sleep(baseDwellMs);
                        }

                        // ── SILENT PHASE: kill DDS output ──
                        AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0));
                        UpdateTDMStatus(cycle, -1, allBands.Count, false);
                        Thread.Sleep(pulseOffTimeMs);

                        // Reset DDS tracking so first band of next cycle reconfigures
                        // (ensures clean state after silent gap)
                        currentBW = -1;
                        currentTones = -1;

                        if (cycle % 100 == 0)
                            Debug.WriteLine("| Cycle " + cycle + "/" + pulseCycles);
                    }
"@

if (Apply-Patch ([ref]$source) $oldB $newB "Pulsed TDM per-band DDS reconfig") { $patchCount++ }


# ═══════════════════════════════════════════════════════════════
# PATCH C: Modify CONTINUOUS TDM loop — reconfigure DDS per band
# ═══════════════════════════════════════════════════════════════
Write-Host "PATCH C: Continuous TDM loop reconfigures DDS per band..."

$oldC = @"
                    // ══════════════════════════════════════
                    // CONTINUOUS TDM MODE
                    // ══════════════════════════════════════
                    Debug.WriteLine("│ Mode: CONTINUOUS TDM");
                    int dwellMs = Math.Max(5, 50 / allBands.Count);

                    while (isJamming)
                    {
                        for (int i = 0; i < allBands.Count && isJamming; i++)
                        {
                            HopToFrequency(allBands[i].FrequencyMHz);
                            UpdateTDMStatus(0, i, allBands.Count, true);
                            Thread.Sleep(dwellMs);
                        }
                    }
"@

$newC = @"
                    // ══════════════════════════════════════
                    // CONTINUOUS TDM MODE
                    // ══════════════════════════════════════
                    Debug.WriteLine("| Mode: CONTINUOUS TDM");
                    int dwellMs = Math.Max(5, 50 / allBands.Count);

                    double contCurrentBW = -1;
                    int contCurrentTones = -1;

                    while (isJamming)
                    {
                        for (int i = 0; i < allBands.Count && isJamming; i++)
                        {
                            // Reconfigure DDS only when BW or Tones change
                            if (allBands[i].BandwidthMHz != contCurrentBW || allBands[i].Tones != contCurrentTones)
                            {
                                ReconfigureDDSBandParams(allBands[i]);
                                contCurrentBW = allBands[i].BandwidthMHz;
                                contCurrentTones = allBands[i].Tones;
                                Thread.Sleep(2);
                            }

                            HopToFrequency(allBands[i].FrequencyMHz);
                            UpdateTDMStatus(0, i, allBands.Count, true);
                            Thread.Sleep(dwellMs);
                        }
                    }
"@

if (Apply-Patch ([ref]$source) $oldC $newC "Continuous TDM per-band DDS reconfig") { $patchCount++ }


# ═══════════════════════════════════════════════════════════════
# PATCH D: Update BandConfiguration to track per-band DDS state
#          and add a note for SA measurement reference
# ═══════════════════════════════════════════════════════════════
Write-Host "PATCH D: Update BandConfiguration class..."

$oldD = @"
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
"@

$newD = @"
        /// <summary>
        /// Band configuration class for TDM.
        /// 
        /// SA measurement guide per band:
        ///   Centre = FrequencyMHz
        ///   Span   = BandwidthMHz x 2
        ///   RBW    = 100 kHz or narrower
        ///   Trace  = MAX HOLD across several pulse cycles
        ///   
        /// Points = Tones x 16 (min 32):
        ///   Tones=4  -> 64pts  -> coarse comb (~BW/4 spacing on SA)
        ///   Tones=8  -> 128pts -> visible steps
        ///   Tones=16 -> 256pts -> smooth fill
        ///   Tones=32 -> 512pts -> fine fill
        ///   Tones=64 -> 1024pts -> max density
        /// </summary>
        private class BandConfiguration
        {
            public string Name { get; set; }
            public double FrequencyMHz { get; set; }
            public double BandwidthMHz { get; set; }
            public int Tones { get; set; }
            public int RowIndex { get; set; }
        }
"@

if (Apply-Patch ([ref]$source) $oldD $newD "Update BandConfiguration class") { $patchCount++ }


# ═══════════════════════════════════════════════════════════════
# WRITE OUTPUT
# ═══════════════════════════════════════════════════════════════
[System.IO.File]::WriteAllText($OutputFile, $source, [System.Text.Encoding]::UTF8)

Write-Host ""
Write-Host "===================" -ForegroundColor Cyan
Write-Host "Patches applied: $patchCount / $totalPatches" -ForegroundColor $(if ($patchCount -eq $totalPatches) { "Green" } else { "Yellow" })
Write-Host "Output: $OutputFile"
Write-Host "===================" -ForegroundColor Cyan

if ($patchCount -eq $totalPatches) {
    Write-Host ""
    Write-Host "All patches applied." -ForegroundColor Green
    Write-Host ""
    Write-Host "What changed:" -ForegroundColor White
    Write-Host "  * DDS registers written once at jamming start (one-time setup)"
    Write-Host "  * Sweep params (BW/Tones) reconfigured per-band during TDM hops"
    Write-Host "  * Reconfigure only triggers when BW or Tones differ (fast skip)"
    Write-Host "  * Tones minimum lowered: 4 tones = 64pts (was clamped to 256)"
    Write-Host "  * Left-side DDS display updates per-band during jamming"
    Write-Host ""
    Write-Host "SA verification:" -ForegroundColor White
    Write-Host "  * Each band should now show its OWN bandwidth (not first band's)"
    Write-Host "  * 4 tones = visible comb teeth    8 = coarse fill"
    Write-Host "  * 16 = smooth fill                32+ = dense block"
} else {
    Write-Host ""
    Write-Host "Some patches failed. Check if FmMain was already modified." -ForegroundColor Yellow
}
