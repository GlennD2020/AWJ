// ============================================================================
// WIDENED TONE PATTERN GENERATORS
// Add these methods to your AD9106PatternGenerator class
// 
// Purpose: Increase effective jamming bandwidth per tone from ~1 MHz (CW)
//          to 2-3 MHz, closing gaps between tones in guard bands without
//          needing to increase tone count.
//
// Current:  Pure CW tone = narrow spike = ~1 MHz effective
// New:      "Fat tone" = spread energy = 2-3 MHz effective
// ============================================================================

// Required at top of file:
// using System;

// Add this field to your AD9106PatternGenerator class if not already present:
// private Random rng = new Random();


/// <summary>
/// Generate NOISE-MODULATED multi-tone pattern
/// Each tone has random frequency jitter, spreading energy ~2 MHz per tone.
/// Same tone count covers more bandwidth = higher hit rate on drone hops.
/// 
/// spreadFactor: 0.0 = pure CW (no spread), 1.0 = maximum spread
///               Recommended: 0.3-0.5 for guard band use
/// </summary>
public uint[] GenerateNoiseModulatedTones(int numTones, int numPoints = 4096, double spreadFactor = 0.4)
{
    uint[] pattern = new uint[numPoints];
    int samplesPerTone = numPoints / numTones;

    for (int i = 0; i < numPoints; i++)
    {
        int toneIndex = i / samplesPerTone;
        if (toneIndex >= numTones) toneIndex = numTones - 1;

        // Base frequency for this tone (evenly distributed across 12-bit range)
        double baseFreq = (4095.0 / (numTones - 1)) * toneIndex;

        // Add random jitter proportional to tone spacing
        double toneSpacing = 4095.0 / (numTones - 1);
        double jitter = (rng.NextDouble() - 0.5) * toneSpacing * spreadFactor;

        double value = baseFreq + jitter;

        // Clamp to valid range
        if (value < 0) value = 0;
        if (value > 4095) value = 4095;

        pattern[i] = (uint)value & 0x0FFF;
    }

    return pattern;
}


/// <summary>
/// Generate MINI-CHIRP multi-tone pattern
/// Each tone sweeps a small range (micro-chirp) instead of sitting on one point.
/// Effective bandwidth ~3 MHz per tone depending on chirpWidth.
/// 
/// chirpWidth: Fraction of tone spacing each tone sweeps.
///             0.0 = pure CW, 1.0 = each tone sweeps entire gap to next tone
///             Recommended: 0.5-0.8 for guard band use
/// </summary>
public uint[] GenerateMiniChirpTones(int numTones, int numPoints = 4096, double chirpWidth = 0.6)
{
    uint[] pattern = new uint[numPoints];
    int samplesPerTone = numPoints / numTones;

    for (int i = 0; i < numPoints; i++)
    {
        int toneIndex = i / samplesPerTone;
        if (toneIndex >= numTones) toneIndex = numTones - 1;

        // Base frequency for this tone
        double baseFreq = (4095.0 / (numTones - 1)) * toneIndex;

        // Calculate sweep range for this tone
        double toneSpacing = 4095.0 / (numTones - 1);
        double sweepRange = toneSpacing * chirpWidth;

        // Position within this tone's samples (0.0 to 1.0)
        int sampleWithinTone = i % samplesPerTone;
        double progress = (double)sampleWithinTone / samplesPerTone;

        // Triangle sweep: up then down within each tone's time slot
        // This means the tone sweeps from (base - range/2) to (base + range/2) and back
        double sweepPosition;
        if (progress < 0.5)
            sweepPosition = progress * 2.0;        // 0 to 1 (sweep up)
        else
            sweepPosition = (1.0 - progress) * 2.0; // 1 to 0 (sweep down)

        double value = baseFreq + (sweepPosition - 0.5) * sweepRange;

        // Clamp to valid range
        if (value < 0) value = 0;
        if (value > 4095) value = 4095;

        pattern[i] = (uint)value & 0x0FFF;
    }

    return pattern;
}


/// <summary>
/// Generate HYBRID multi-tone pattern (RECOMMENDED for guard band jamming)
/// Combines mini-chirp sweep with noise overlay for maximum effective bandwidth.
/// Each tone sweeps a range AND has random jitter = ~3-4 MHz effective per tone.
/// 
/// chirpWidth: Sweep range as fraction of tone spacing (0.5-0.8 recommended)
/// noiseFactor: Random jitter amount (0.1-0.3 recommended)
/// </summary>
public uint[] GenerateHybridWideTones(int numTones, int numPoints = 4096, 
    double chirpWidth = 0.6, double noiseFactor = 0.2)
{
    uint[] pattern = new uint[numPoints];
    int samplesPerTone = numPoints / numTones;

    for (int i = 0; i < numPoints; i++)
    {
        int toneIndex = i / samplesPerTone;
        if (toneIndex >= numTones) toneIndex = numTones - 1;

        // Base frequency for this tone
        double baseFreq = (4095.0 / (numTones - 1)) * toneIndex;
        double toneSpacing = 4095.0 / (numTones - 1);

        // Mini-chirp component
        double sweepRange = toneSpacing * chirpWidth;
        int sampleWithinTone = i % samplesPerTone;
        double progress = (double)sampleWithinTone / samplesPerTone;

        // Triangle sweep within each tone
        double sweepPosition;
        if (progress < 0.5)
            sweepPosition = progress * 2.0;
        else
            sweepPosition = (1.0 - progress) * 2.0;

        double chirpOffset = (sweepPosition - 0.5) * sweepRange;

        // Noise overlay component
        double noiseOffset = (rng.NextDouble() - 0.5) * toneSpacing * noiseFactor;

        // Combine
        double value = baseFreq + chirpOffset + noiseOffset;

        // Clamp to valid range
        if (value < 0) value = 0;
        if (value > 4095) value = 4095;

        pattern[i] = (uint)value & 0x0FFF;
    }

    return pattern;
}


// ============================================================================
// UPDATED ExecuteMultiToneMode — drop-in replacement
// Replace your existing ExecuteMultiToneMode with this version
// Adds waveform type selection based on band configuration
// ============================================================================

/*
private void ExecuteMultiToneMode(DataGridViewRow row, string bandName, 
    double startFreqMHz, double bandwidthMHz, int dacChannel)
{
    if (dgvBands.Columns.Contains("colStatus"))
    {
        row.Cells["colStatus"].Value = "LOADING WIDE-TONE...";
        dgvBands.Refresh();
    }

    int numTones = CalculateOptimalTones(row, bandName, bandwidthMHz);
    
    // *** NEW: Select waveform type based on guard band context ***
    // Guard bands need wide tones for gap-free coverage
    // Other bands can use standard CW multi-tone
    
    bool isGuardBand = bandName.Contains("Guard");
    string waveformType = isGuardBand ? "HYBRID" : "CW";
    
    string patternKey = bandName + "_" + waveformType + "_" + numTones + "_4096";

    if (!loadedPatterns.ContainsKey(patternKey))
    {
        Debug.WriteLine("┌─ Generating " + waveformType + " MULTI-TONE: " + bandName);
        Debug.WriteLine("│   Tones: " + numTones + " | Spacing: " + 
            (bandwidthMHz / numTones).ToString("F2") + " MHz");

        uint[] pattern;
        
        if (isGuardBand)
        {
            // HYBRID wide tones for guard bands — maximum hop interception
            // chirpWidth=0.6 means each tone sweeps 60% of the gap to next tone
            // noiseFactor=0.2 adds random jitter on top
            // Result: ~3-4 MHz effective bandwidth per tone
            pattern = patternGen.GenerateHybridWideTones(numTones, 4096, 0.6, 0.2);
            
            double effectiveBwPerTone = (bandwidthMHz / numTones) * (0.6 + 0.2);
            double totalCoverage = effectiveBwPerTone * numTones;
            double hitRate = Math.Min(100.0, (totalCoverage / bandwidthMHz) * 100.0);
            
            Debug.WriteLine("│   Waveform: HYBRID (mini-chirp + noise)");
            Debug.WriteLine("│   Effective BW per tone: ~" + 
                effectiveBwPerTone.ToString("F1") + " MHz");
            Debug.WriteLine("│   Estimated hop hit rate: ~" + 
                hitRate.ToString("F0") + "%");
        }
        else
        {
            // Standard CW multi-tone for non-guard bands
            pattern = patternGen.GenerateMultiTonePattern(numTones, 4096);
            Debug.WriteLine("│   Waveform: CW (standard multi-tone)");
        }
        
        loadedPatterns[patternKey] = pattern;
        AddSequences(new SequenceLoadPatternMemory(ltTxCommand, null, pattern));
        Debug.WriteLine("│ ✓ Pattern loaded: " + numTones + " simultaneous frequencies");
    }
    else
    {
        Debug.WriteLine("┌─ Using cached " + waveformType + ": " + patternKey);
        AddSequences(new SequenceLoadPatternMemory(ltTxCommand, null, 
            loadedPatterns[patternKey]));
    }

    AddSequences(new SequenceConfigurePattern(ltTxCommand, null, dacChannel, 0, 4095, 0));
    AddSequences(new SequenceUpdateConfig(ltTxCommand, null));

    if (dgvBands.Columns.Contains("colStatus"))
    {
        string status = isGuardBand ? "WIDE-TONE READY" : "MULTI-TONE READY";
        row.Cells["colStatus"].Value = status + " (" + numTones + "T)";
        dgvBands.Refresh();
    }

    AddSequences(new SequenceTriggerPattern(ltTxCommand, null));
    
    Debug.WriteLine("│ " + waveformType + ": " + numTones + " tones × " + 
        bandwidthMHz.ToString("F1") + " MHz | Time: 22.76 μs");

    Thread.Sleep(1);
    AddSequences(new SequenceStopPattern(ltTxCommand, null));
}
*/


// ============================================================================
// COMPARISON: What each waveform looks like on a spectrum analyser
// ============================================================================
//
// CURRENT (CW Multi-Tone) — 4 tones across 10 MHz:
//
//    Power
//    ▲
//    │   │       │       │       │
//    │   │       │       │       │
//    │   │       │       │       │
//    └───┼───────┼───────┼───────┼──── Freq (MHz)
//      2402    2404    2406    2408
//
//    Each tone is a narrow spike ~1 MHz wide
//    Gaps between tones = drone hops slip through
//
//
// NEW (Hybrid Wide-Tone) — same 4 tones across 10 MHz:
//
//    Power
//    ▲
//    │ ▓▓▓▓▓   ▓▓▓▓▓   ▓▓▓▓▓   ▓▓▓▓▓
//    │ ▓▓▓▓▓   ▓▓▓▓▓   ▓▓▓▓▓   ▓▓▓▓▓
//    │ ▓▓▓▓▓   ▓▓▓▓▓   ▓▓▓▓▓   ▓▓▓▓▓
//    └─▓▓▓▓▓───▓▓▓▓▓───▓▓▓▓▓───▓▓▓▓▓── Freq (MHz)
//      2401-   2403-   2405-   2407-
//      2403    2405    2407    2409
//
//    Each tone spreads ~2.5 MHz = overlapping coverage
//    No gaps = every drone hop intercepted
//
// ============================================================================
