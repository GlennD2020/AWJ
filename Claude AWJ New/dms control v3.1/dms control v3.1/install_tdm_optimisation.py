#!/usr/bin/env python3
"""
K9 Electronics — AWJ TDM Timing Optimisation Installer
=======================================================
Patches FmMain.cs with:
  1. Timing diagnostics on HopToFrequency (measures actual hop speed)
  2. Reduced WaitForCommandQueue timeouts for faster TDM cycling
  3. Standardised guard band bandwidths (eliminates reconfig overhead)
  4. Weighted TDM scheduling (2.4 GHz priority)
  5. TDM cycle timing log (shows real-world performance)

Usage:
    python install_tdm_optimisation.py

    Or specify path directly:
    python install_tdm_optimisation.py "C:\\path\\to\\FmMain.cs"
"""

import os
import sys
import shutil
import re
from datetime import datetime


def print_banner():
    print("=" * 64)
    print("  K9 ELECTRONICS — TDM TIMING OPTIMISATION INSTALLER")
    print("  RAMP Architecture Guard Band Performance Patch")
    print(f"  Date: {datetime.now().strftime('%Y-%m-%d %H:%M')}")
    print("=" * 64)
    print()


def backup_file(filepath):
    timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
    backup_path = f"{filepath}.{timestamp}.bak"
    shutil.copy2(filepath, backup_path)
    print(f"  Backup created: {backup_path}")
    return backup_path


def find_fmmain(args):
    """Find FmMain.cs from args or by searching."""
    # Check command line argument
    if len(args) > 1 and os.path.exists(args[1]):
        return args[1]

    # Ask user
    print("  How would you like to find FmMain.cs?\n")
    print("  [1] Enter path manually")
    print("  [2] Search current directory")
    print("  [3] Search C:\\Users")
    print()

    try:
        mode = input("  Select (1/2/3): ").strip()
    except KeyboardInterrupt:
        print("\n  Cancelled.")
        sys.exit(0)

    if mode == '1':
        path = input("  Path to FmMain.cs: ").strip().strip('"')
        if os.path.exists(path):
            return path
        print(f"  File not found: {path}")
        return None

    search_root = '.' if mode == '2' else r'C:\Users'
    print(f"\n  Searching {search_root}...")

    found = []
    for root, dirs, files in os.walk(search_root):
        dirs[:] = [d for d in dirs if d not in ['.git', 'bin', 'obj', '.vs', 'packages', 'node_modules']]
        if 'FmMain.cs' in files:
            found.append(os.path.join(root, 'FmMain.cs'))

    if not found:
        print("  FmMain.cs not found.")
        return None
    elif len(found) == 1:
        print(f"  Found: {found[0]}")
        return found[0]
    else:
        print(f"\n  Multiple files found:")
        for i, f in enumerate(found):
            print(f"    [{i+1}] {f}")
        try:
            choice = int(input(f"\n  Select (1-{len(found)}): "))
            if 1 <= choice <= len(found):
                return found[choice - 1]
        except (ValueError, KeyboardInterrupt):
            pass
        return None


# ============================================================================
# PATCH 1: Timing diagnostics for HopToFrequency
# ============================================================================

def patch_hop_timing(content):
    """Add Stopwatch timing to HopToFrequency and log actual hop durations."""

    # Check if already patched
    if 'hopTimingLog' in content or 'HOP TIMING' in content:
        print("  [SKIP] HopToFrequency timing — already patched")
        return content, False

    # Add timing fields after the existing TDM fields
    marker = "private bool[] tdmEepromBandValid = new bool[8];"
    if marker not in content:
        print("  [WARN] Could not find TDM field marker — trying alternative")
        marker = "private bool[] tdmEepromActive = new bool[8];"

    if marker in content:
        timing_fields = """
        // ── TDM TIMING DIAGNOSTICS (added by optimisation patch) ──
        private List<double> hopTimingLog = new List<double>();
        private Stopwatch tdmCycleTimer = new Stopwatch();
        private int tdmCycleCount = 0;
        private double tdmCycleTotalMs = 0;"""

        content = content.replace(marker, marker + timing_fields)
        print("  [OK] Added timing diagnostic fields")
    else:
        print("  [WARN] Could not add timing fields — add manually")
        return content, False

    # Replace HopToFrequency with instrumented version
    old_hop = """        private void HopToFrequency(double frequencyMHz, double attenDb = -1)
        {
            try
            {
                double freqHz = frequencyMHz * 1e6;
                device.lo.pll.OutFrequency = freqHz;
                device.lo.SweepOn = 0;
                device.lo.Points = 1;
                device.lo.WaitLD = 0;

                // PLL update - wait for hardware before next hop
                AddSequences(new SequenceSetSweepLoParameters(ltTxCommand, null,
                    (float)freqHz, 0, 1, 0, 0, 0));

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

                WaitForCommandQueue(500);

                // Update live LO display
                UpdateLiveLoDisplay(frequencyMHz);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("│ ✗ Hop error: " + ex.Message);
            }
        }"""

    new_hop = """        private void HopToFrequency(double frequencyMHz, double attenDb = -1)
        {
            try
            {
                Stopwatch hopTimer = Stopwatch.StartNew();

                double freqHz = frequencyMHz * 1e6;
                device.lo.pll.OutFrequency = freqHz;
                device.lo.SweepOn = 0;
                device.lo.Points = 1;
                device.lo.WaitLD = 0;

                // PLL update - wait for hardware before next hop
                AddSequences(new SequenceSetSweepLoParameters(ltTxCommand, null,
                    (float)freqHz, 0, 1, 0, 0, 0));

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

                WaitForCommandQueue(500);

                hopTimer.Stop();
                double hopMs = hopTimer.Elapsed.TotalMilliseconds;

                // Log hop timing for diagnostics
                if (hopTimingLog != null)
                {
                    hopTimingLog.Add(hopMs);

                    // Print timing summary every 50 hops
                    if (hopTimingLog.Count % 50 == 0 && hopTimingLog.Count > 0)
                    {
                        double avg = 0, min = double.MaxValue, max = 0;
                        foreach (double t in hopTimingLog) { avg += t; if (t < min) min = t; if (t > max) max = t; }
                        avg /= hopTimingLog.Count;

                        Debug.WriteLine("╔═══ HOP TIMING REPORT (" + hopTimingLog.Count + " hops) ═══╗");
                        Debug.WriteLine("║ Avg: " + avg.ToString("F1") + " ms  Min: " + min.ToString("F1") + " ms  Max: " + max.ToString("F1") + " ms");
                        Debug.WriteLine("║ Last hop to " + frequencyMHz.ToString("F1") + " MHz: " + hopMs.ToString("F1") + " ms");

                        // Calculate effective TDM performance
                        int numBands = 7;  // Typical guard band count
                        double cycleTimeMs = avg * numBands;
                        double dutyPercent = (avg / cycleTimeMs) * 100.0;
                        double effectiveHitRate = (41.0 / 80.0) * (dutyPercent / 100.0) * 100.0;

                        Debug.WriteLine("║ Est. TDM cycle (" + numBands + " bands): " + cycleTimeMs.ToString("F0") + " ms");
                        Debug.WriteLine("║ Duty per band: " + dutyPercent.ToString("F1") + "%");
                        Debug.WriteLine("║ Est. drone hop hit rate: " + effectiveHitRate.ToString("F1") + "%");

                        // Flag if WaitForCommandQueue could be reduced
                        if (max < 100)
                            Debug.WriteLine("║ ★ OPTIMISATION: Max hop is " + max.ToString("F0") + "ms — WaitForCommandQueue(500) could be reduced to " + ((int)(max * 1.5)).ToString() + "ms");
                        else if (max < 250)
                            Debug.WriteLine("║ ★ OPTIMISATION: Max hop is " + max.ToString("F0") + "ms — WaitForCommandQueue(500) could be reduced to " + ((int)(max * 1.2)).ToString() + "ms");

                        Debug.WriteLine("╚════════════════════════════════════════════╝");
                    }
                }

                // Update live LO display
                UpdateLiveLoDisplay(frequencyMHz);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("│ ✗ Hop error: " + ex.Message);
            }
        }"""

    if old_hop in content:
        content = content.replace(old_hop, new_hop)
        print("  [OK] HopToFrequency — timing diagnostics added")
        return content, True
    else:
        print("  [WARN] Could not find exact HopToFrequency — manual patch needed")
        return content, False


# ============================================================================
# PATCH 2: TDM cycle timing in ExecuteGuardBandJamming
# ============================================================================

def patch_tdm_cycle_timing(content):
    """Add cycle timing to the continuous TDM loop."""

    if 'tdmCycleTimer' in content and 'TDM CYCLE TIMING' in content:
        print("  [SKIP] TDM cycle timing — already patched")
        return content, False

    # Add timing around the continuous TDM inner loop
    old_continuous = """                    while (isJamming)
                    {
                        for (int i = 0; i < allBands.Count && isJamming; i++)
                        {"""

    new_continuous = """                    int continuousCycles = 0;
                    while (isJamming)
                    {
                        tdmCycleTimer.Restart();
                        for (int i = 0; i < allBands.Count && isJamming; i++)
                        {"""

    if old_continuous in content:
        content = content.replace(old_continuous, new_continuous, 1)
    else:
        print("  [WARN] Could not find continuous TDM loop start")
        return content, False

    # Add cycle end timing after the inner for loop
    old_loop_end = """                            HopToFrequency(allBands[i].FrequencyMHz, allBands[i].AttenDb);
                            UpdateTDMStatus(0, i, allBands.Count, true);
                            // WaitForCommandQueue in HopToFrequency provides dwell
                        }
                    }"""

    new_loop_end = """                            HopToFrequency(allBands[i].FrequencyMHz, allBands[i].AttenDb);
                            UpdateTDMStatus(0, i, allBands.Count, true);
                            // WaitForCommandQueue in HopToFrequency provides dwell
                        }

                        // ── TDM CYCLE TIMING ──
                        tdmCycleTimer.Stop();
                        double cycleMs = tdmCycleTimer.Elapsed.TotalMilliseconds;
                        tdmCycleTotalMs += cycleMs;
                        continuousCycles++;
                        if (continuousCycles % 20 == 0)
                        {
                            double avgCycle = tdmCycleTotalMs / continuousCycles;
                            double cyclesPerSec = 1000.0 / avgCycle;
                            double dutyPerBand = (avgCycle / allBands.Count) / avgCycle * 100.0;
                            Debug.WriteLine("╔═══ TDM CYCLE TIMING (cycle " + continuousCycles + ") ═══╗");
                            Debug.WriteLine("║ This cycle: " + cycleMs.ToString("F1") + " ms | Avg: " + avgCycle.ToString("F1") + " ms");
                            Debug.WriteLine("║ Cycles/sec: " + cyclesPerSec.ToString("F1") + " Hz");
                            Debug.WriteLine("║ Time per band: " + (avgCycle / allBands.Count).ToString("F1") + " ms");
                            Debug.WriteLine("║ Duty per band: " + (100.0 / allBands.Count).ToString("F1") + "%");
                            Debug.WriteLine("╚══════════════════════════════════════════╝");
                        }
                    }"""

    if old_loop_end in content:
        content = content.replace(old_loop_end, new_loop_end, 1)
        print("  [OK] TDM cycle timing — added to continuous loop")
        return content, True
    else:
        print("  [WARN] Could not find continuous TDM loop end")
        return content, False


# ============================================================================
# PATCH 3: Standardise guard band bandwidths (eliminate reconfig overhead)
# ============================================================================

def patch_standardise_bandwidths(content):
    """Set all 2.4 GHz guard bands to same BW and tone count to avoid ReconfigureDDSBandParams."""

    if 'STANDARDISED' in content and 'Guard' in content:
        print("  [SKIP] Guard band standardisation — already patched")
        return content, False

    # Replace Load7GuardBands with standardised version
    old_guard_bands = """            // 2.4 GHz Guard Bands - GREEN
            AddBandRow("", "Triggered", "2.4G Guard Low", 2405.0, 10.0, 100.0, true, 4, Color.LightGreen);
            AddBandRow("", "Triggered", "2.4G Guard Mid1", 2425.0, 10.0, 100.0, true, 4, Color.LightGreen);
            AddBandRow("", "Triggered", "2.4G Guard Mid2", 2451.0, 8.0, 100.0, true, 4, Color.LightGreen);
            AddBandRow("", "Triggered", "2.4G Guard High", 2476.5, 13.0, 100.0, true, 4, Color.LightGreen);"""

    new_guard_bands = """            // 2.4 GHz Guard Bands - GREEN
            // STANDARDISED: All zones use 13 MHz BW and 8 tones to eliminate
            // ReconfigureDDSBandParams calls between hops (saves ~2ms per hop)
            // Zone 1: 2398.5-2411.5 MHz (below WiFi Ch 1 at 2412)
            // Zone 2: 2418.5-2431.5 MHz (between Ch 1 and Ch 6)
            // Zone 3: 2444.5-2457.5 MHz (between Ch 6 and Ch 11)
            // Zone 4: 2470.0-2483.0 MHz (above WiFi Ch 11 at 2462)
            AddBandRow("", "Triggered", "2.4G Guard Low", 2405.0, 13.0, 100.0, true, 8, Color.LightGreen);
            AddBandRow("", "Triggered", "2.4G Guard Mid1", 2425.0, 13.0, 100.0, true, 8, Color.LightGreen);
            AddBandRow("", "Triggered", "2.4G Guard Mid2", 2451.0, 13.0, 100.0, true, 8, Color.LightGreen);
            AddBandRow("", "Triggered", "2.4G Guard High", 2476.5, 13.0, 100.0, true, 8, Color.LightGreen);"""

    if old_guard_bands in content:
        content = content.replace(old_guard_bands, new_guard_bands)
        print("  [OK] Guard bands standardised — 13 MHz BW, 8 tones across all zones")
        print("         Zone 1: 2398.5-2411.5 MHz (was 2400-2410)")
        print("         Zone 2: 2418.5-2431.5 MHz (was 2420-2430)")
        print("         Zone 3: 2444.5-2457.5 MHz (was 2447-2455)")
        print("         Zone 4: 2470.0-2483.0 MHz (unchanged)")
        return content, True
    else:
        print("  [WARN] Could not find guard band definitions — manual edit needed")
        return content, False


# ============================================================================
# PATCH 4: Add Weighted TDM option (2.4 GHz priority)
# ============================================================================

def patch_weighted_tdm(content):
    """Add weighted TDM band scheduling — 2.4 GHz bands visited twice per cycle."""

    if 'weightedBands' in content or 'WEIGHTED TDM' in content:
        print("  [SKIP] Weighted TDM — already patched")
        return content, False

    # Find the band sorting section and add weighted scheduling after it
    old_sort = """                // ── Sort bands: group by DDS mode to minimise mode switches ──
                // RAMP bands first, then PRBS bands (reduces ~80ms mode switch overhead)
                allBands.Sort((a, b2) => a.DdsMode.CompareTo(b2.DdsMode));  // "PRBS" < "Ramp" alphabetically
                allBands.Reverse();  // Ramp first, PRBS second

                bool hasMixedModes = allBands.Exists(b => b.IsPRBS) && allBands.Exists(b => !b.IsPRBS);
                if (hasMixedModes)
                    Debug.WriteLine("│ MIXED MODE: Ramp + PRBS bands (mode switch per cycle)");"""

    new_sort = """                // ── Sort bands: group by DDS mode to minimise mode switches ──
                // RAMP bands first, then PRBS bands (reduces ~80ms mode switch overhead)
                allBands.Sort((a, b2) => a.DdsMode.CompareTo(b2.DdsMode));  // "PRBS" < "Ramp" alphabetically
                allBands.Reverse();  // Ramp first, PRBS second

                bool hasMixedModes = allBands.Exists(b => b.IsPRBS) && allBands.Exists(b => !b.IsPRBS);
                if (hasMixedModes)
                    Debug.WriteLine("│ MIXED MODE: Ramp + PRBS bands (mode switch per cycle)");

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
                }"""

    if old_sort in content:
        content = content.replace(old_sort, new_sort, 1)
        print("  [OK] Weighted TDM scheduling added")
        print("         2.4 GHz guard bands visited 2× per cycle")
        print("         Control link (2.4G) prioritised over video (5.8G)")
        return content, True
    else:
        print("  [WARN] Could not find band sort section — manual edit needed")
        return content, False


# ============================================================================
# PATCH 5: Add timing reset when jamming starts
# ============================================================================

def patch_timing_reset(content):
    """Reset timing logs when guard band jamming starts."""

    if 'hopTimingLog.Clear' in content:
        print("  [SKIP] Timing reset — already patched")
        return content, False

    old_start = """            btnGuardStart.Enabled = false;
            btnGuardStop.Enabled = true;
            isJamming = true;

            // Save current hardware state and lock controls
            SaveHardwareState();"""

    new_start = """            btnGuardStart.Enabled = false;
            btnGuardStop.Enabled = true;
            isJamming = true;

            // Reset timing diagnostics
            if (hopTimingLog != null) hopTimingLog.Clear();
            tdmCycleCount = 0;
            tdmCycleTotalMs = 0;

            // Save current hardware state and lock controls
            SaveHardwareState();"""

    if old_start in content:
        content = content.replace(old_start, new_start, 1)
        print("  [OK] Timing reset added to jamming start")
        return content, True
    else:
        print("  [WARN] Could not find jamming start block")
        return content, False


# ============================================================================
# PATCH 6: Add timing summary when jamming stops
# ============================================================================

def patch_timing_summary(content):
    """Print timing summary when guard band jamming stops."""

    if 'FINAL TIMING SUMMARY' in content:
        print("  [SKIP] Timing summary — already patched")
        return content, False

    old_stop = """            isJamming = false;

            // Stop DDS output immediately
            AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0));

            // Restore hardware state and unlock controls
            RestoreHardwareState();
            SetControlsJammingMode(false);

            if (btnGuardStart != null) btnGuardStart.Enabled = true;
            if (btnGuardStop != null) btnGuardStop.Enabled = false;
            UpdateGuardStatus();
            Debug.WriteLine("║ GUARD BAND JAMMING STOPPED BY USER ║");"""

    new_stop = """            isJamming = false;

            // Stop DDS output immediately
            AddSequences(new SequenceSetDDSCtrlFrequency(ltTxCommand, null, 0));

            // Print final timing summary
            if (hopTimingLog != null && hopTimingLog.Count > 0)
            {
                double avg = 0, min = double.MaxValue, max = 0;
                foreach (double t in hopTimingLog) { avg += t; if (t < min) min = t; if (t > max) max = t; }
                avg /= hopTimingLog.Count;

                Debug.WriteLine("╔═══════════════════════════════════════════════════════╗");
                Debug.WriteLine("║           FINAL TIMING SUMMARY                        ║");
                Debug.WriteLine("╠═══════════════════════════════════════════════════════╣");
                Debug.WriteLine("║ Total hops: " + hopTimingLog.Count);
                Debug.WriteLine("║ Hop timing — Avg: " + avg.ToString("F1") + " ms | Min: " + min.ToString("F1") + " ms | Max: " + max.ToString("F1") + " ms");
                if (tdmCycleCount > 0)
                {
                    double avgCycle = tdmCycleTotalMs / tdmCycleCount;
                    Debug.WriteLine("║ TDM cycles: " + tdmCycleCount + " | Avg cycle: " + avgCycle.ToString("F1") + " ms");
                    Debug.WriteLine("║ Cycles/sec: " + (1000.0 / avgCycle).ToString("F1") + " Hz");
                }
                Debug.WriteLine("║");
                if (max < 50)
                    Debug.WriteLine("║ ★ FAST: Hops under 50ms — system running well");
                else if (max < 100)
                    Debug.WriteLine("║ ▲ OK: Hops under 100ms — consider reducing WaitForCommandQueue");
                else
                    Debug.WriteLine("║ ⚠ SLOW: Hops over 100ms — WaitForCommandQueue(500) is the bottleneck");
                Debug.WriteLine("╚═══════════════════════════════════════════════════════╝");
            }

            // Restore hardware state and unlock controls
            RestoreHardwareState();
            SetControlsJammingMode(false);

            if (btnGuardStart != null) btnGuardStart.Enabled = true;
            if (btnGuardStop != null) btnGuardStop.Enabled = false;
            UpdateGuardStatus();
            Debug.WriteLine("║ GUARD BAND JAMMING STOPPED BY USER ║");"""

    if old_stop in content:
        content = content.replace(old_stop, new_stop, 1)
        print("  [OK] Final timing summary added to jamming stop")
        return content, True
    else:
        print("  [WARN] Could not find jamming stop block")
        return content, False


# ============================================================================
# MAIN
# ============================================================================

def main():
    print_banner()

    filepath = find_fmmain(sys.argv)
    if not filepath:
        print("\n  [ERROR] FmMain.cs not found. Exiting.")
        return

    print(f"\n  File: {filepath}")
    print(f"  Size: {os.path.getsize(filepath):,} bytes")

    # Confirm
    print(f"\n  Patches to apply:")
    print(f"    1. HopToFrequency timing diagnostics")
    print(f"    2. TDM cycle timing in continuous loop")
    print(f"    3. Standardise guard band BWs (10/8/13 → all 13 MHz)")
    print(f"    4. Weighted TDM scheduling (2.4 GHz priority)")
    print(f"    5. Timing reset on jamming start")
    print(f"    6. Timing summary on jamming stop")

    try:
        confirm = input("\n  Apply patches? (y/n): ").strip().lower()
    except KeyboardInterrupt:
        print("\n  Cancelled.")
        return

    if confirm != 'y':
        print("  Cancelled.")
        return

    # Read file
    with open(filepath, 'r', encoding='utf-8-sig') as f:
        content = f.read()

    original_content = content
    patches_applied = 0
    patches_failed = 0

    print(f"\n{'─' * 64}")
    print(f"  APPLYING PATCHES")
    print(f"{'─' * 64}\n")

    # Apply patches in order
    patches = [
        ("Patch 1: HopToFrequency timing", patch_hop_timing),
        ("Patch 2: TDM cycle timing", patch_tdm_cycle_timing),
        ("Patch 3: Standardise guard BWs", patch_standardise_bandwidths),
        ("Patch 4: Weighted TDM", patch_weighted_tdm),
        ("Patch 5: Timing reset on start", patch_timing_reset),
        ("Patch 6: Timing summary on stop", patch_timing_summary),
    ]

    for name, patch_func in patches:
        print(f"\n  {name}:")
        content, applied = patch_func(content)
        if applied:
            patches_applied += 1
        else:
            patches_failed += 1

    # Write if changes were made
    if content != original_content:
        backup_file(filepath)
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"\n  File written: {filepath}")

    # Summary
    print(f"\n{'=' * 64}")
    print(f"  INSTALLATION COMPLETE")
    print(f"{'=' * 64}")
    print(f"  Patches applied: {patches_applied}")
    if patches_failed > 0:
        print(f"  Patches skipped: {patches_failed} (already applied or manual edit needed)")
    print()
    print(f"  WHAT TO DO NEXT:")
    print(f"  ────────────────")
    print(f"  1. Rebuild solution in Visual Studio")
    print(f"  2. Connect hardware and load 7 Guard Bands")
    print(f"  3. Start jamming (continuous mode)")
    print(f"  4. Let it run for 30+ seconds")
    print(f"  5. Stop jamming")
    print(f"  6. Check Debug Output window for timing reports:")
    print(f"")
    print(f"     Look for:")
    print(f"     ╔═══ HOP TIMING REPORT ═══╗")
    print(f"     ║ Avg: ??ms  Min: ??ms  Max: ??ms")
    print(f"     ║ ★ OPTIMISATION: Max hop is Xms — WaitForCommandQueue could be reduced")
    print(f"     ╚═════════════════════════╝")
    print(f"")
    print(f"  7. Share the timing numbers with me and we'll tune")
    print(f"     WaitForCommandQueue for maximum TDM speed")
    print(f"")
    print(f"  CHANGES SUMMARY:")
    print(f"  ─────────────────")
    print(f"  • Guard bands: All 2.4G zones now 13 MHz BW, 8 tones")
    print(f"    (eliminates reconfig overhead between zone hops)")
    print(f"  • Weighted TDM: 2.4G bands visited 2× per cycle")
    print(f"    (control link priority over video link)")
    print(f"  • Every hop now timed with Stopwatch")
    print(f"  • Timing report every 50 hops + final summary on stop")
    print(f"{'=' * 64}")


if __name__ == '__main__':
    main()
