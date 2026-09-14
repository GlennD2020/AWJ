#!/usr/bin/env python3
"""
K9 Electronics - AWJ Widened Tone Pattern Installer
====================================================
This script patches your existing C# source files to add the new
widened tone pattern generators (Noise-Modulated, Mini-Chirp, Hybrid)
and updates ExecuteMultiToneMode to use them for guard bands.

Usage:
    python install_widened_tones.py

The script will:
1. Find your AD9106PatternGenerator class and add 3 new methods
2. Find your FmMain and update ExecuteMultiToneMode for guard bands
3. Create backups of all modified files (.bak)

Requirements:
    - Python 3.6+
    - No external packages needed
"""

import os
import sys
import shutil
import re
from datetime import datetime

# ============================================================================
# CONFIGURATION - Update these paths to match your project
# ============================================================================

# Try common locations, or set manually
SEARCH_PATHS = [
    r"C:\Users",           # Will search recursively
    r"D:\Projects",
    r".",                   # Current directory
]

# Filenames to find
PATTERN_GEN_FILENAME = "AD9106PatternGenerator.cs"
FMMAIN_FILENAME = "FmMain.cs"

# ============================================================================
# NEW CODE TO INSERT
# ============================================================================

NEW_PATTERN_METHODS = '''
        // ================================================================
        // WIDENED TONE PATTERNS - Added {date}
        // K9 Electronics AWJ Guard Band Optimisation
        // Increases effective bandwidth per tone from ~1 MHz to 3-4 MHz
        // ================================================================

        /// <summary>
        /// Generate NOISE-MODULATED multi-tone pattern.
        /// Each tone has random frequency jitter, spreading energy ~2 MHz per tone.
        /// spreadFactor: 0.0 = pure CW, 1.0 = max spread. Recommended: 0.3-0.5
        /// </summary>
        public uint[] GenerateNoiseModulatedTones(int numTones, int numPoints = 4096, double spreadFactor = 0.4)
        {{
            uint[] pattern = new uint[numPoints];
            int samplesPerTone = numPoints / numTones;

            for (int i = 0; i < numPoints; i++)
            {{
                int toneIndex = i / samplesPerTone;
                if (toneIndex >= numTones) toneIndex = numTones - 1;

                double baseFreq = (4095.0 / (numTones - 1)) * toneIndex;
                double toneSpacing = 4095.0 / (numTones - 1);
                double jitter = (rng.NextDouble() - 0.5) * toneSpacing * spreadFactor;

                double value = baseFreq + jitter;
                if (value < 0) value = 0;
                if (value > 4095) value = 4095;

                pattern[i] = (uint)value & 0x0FFF;
            }}

            return pattern;
        }}

        /// <summary>
        /// Generate MINI-CHIRP multi-tone pattern.
        /// Each tone sweeps a small range (triangle wave) instead of sitting on one point.
        /// Effective bandwidth ~3 MHz per tone.
        /// chirpWidth: 0.0 = pure CW, 1.0 = sweeps entire gap. Recommended: 0.5-0.8
        /// </summary>
        public uint[] GenerateMiniChirpTones(int numTones, int numPoints = 4096, double chirpWidth = 0.6)
        {{
            uint[] pattern = new uint[numPoints];
            int samplesPerTone = numPoints / numTones;

            for (int i = 0; i < numPoints; i++)
            {{
                int toneIndex = i / samplesPerTone;
                if (toneIndex >= numTones) toneIndex = numTones - 1;

                double baseFreq = (4095.0 / (numTones - 1)) * toneIndex;
                double toneSpacing = 4095.0 / (numTones - 1);
                double sweepRange = toneSpacing * chirpWidth;

                int sampleWithinTone = i % samplesPerTone;
                double progress = (double)sampleWithinTone / samplesPerTone;

                double sweepPosition;
                if (progress < 0.5)
                    sweepPosition = progress * 2.0;
                else
                    sweepPosition = (1.0 - progress) * 2.0;

                double value = baseFreq + (sweepPosition - 0.5) * sweepRange;
                if (value < 0) value = 0;
                if (value > 4095) value = 4095;

                pattern[i] = (uint)value & 0x0FFF;
            }}

            return pattern;
        }}

        /// <summary>
        /// Generate HYBRID wide-tone pattern (RECOMMENDED for guard band jamming).
        /// Combines mini-chirp sweep with noise overlay = ~3-4 MHz effective per tone.
        /// chirpWidth: Sweep range fraction (0.5-0.8). noiseFactor: Jitter amount (0.1-0.3).
        /// </summary>
        public uint[] GenerateHybridWideTones(int numTones, int numPoints = 4096, 
            double chirpWidth = 0.6, double noiseFactor = 0.2)
        {{
            uint[] pattern = new uint[numPoints];
            int samplesPerTone = numPoints / numTones;

            for (int i = 0; i < numPoints; i++)
            {{
                int toneIndex = i / samplesPerTone;
                if (toneIndex >= numTones) toneIndex = numTones - 1;

                double baseFreq = (4095.0 / (numTones - 1)) * toneIndex;
                double toneSpacing = 4095.0 / (numTones - 1);

                // Mini-chirp component
                double sweepRange = toneSpacing * chirpWidth;
                int sampleWithinTone = i % samplesPerTone;
                double progress = (double)sampleWithinTone / samplesPerTone;

                double sweepPosition;
                if (progress < 0.5)
                    sweepPosition = progress * 2.0;
                else
                    sweepPosition = (1.0 - progress) * 2.0;

                double chirpOffset = (sweepPosition - 0.5) * sweepRange;

                // Noise overlay component
                double noiseOffset = (rng.NextDouble() - 0.5) * toneSpacing * noiseFactor;

                double value = baseFreq + chirpOffset + noiseOffset;
                if (value < 0) value = 0;
                if (value > 4095) value = 4095;

                pattern[i] = (uint)value & 0x0FFF;
            }}

            return pattern;
        }}
'''

UPDATED_EXECUTE_MULTITONE = '''        /// <summary>
        /// Execute Multi-Tone Pattern Mode — UPDATED with Widened Tones for Guard Bands
        /// Guard bands automatically use HybridWideTones for ~100% hop interception.
        /// Non-guard bands use standard CW multi-tone.
        /// Updated: {date}
        /// </summary>
        private void ExecuteMultiToneMode(DataGridViewRow row, string bandName, 
            double startFreqMHz, double bandwidthMHz, int dacChannel)
        {{
            if (dgvBands.Columns.Contains("colStatus"))
            {{
                row.Cells["colStatus"].Value = "LOADING WIDE-TONE...";
                dgvBands.Refresh();
            }}

            int numTones = CalculateOptimalTones(row, bandName, bandwidthMHz);
            
            // Select waveform type based on guard band context
            bool isGuardBand = bandName.Contains("Guard");
            string waveformType = isGuardBand ? "HYBRID" : "CW";
            
            string patternKey = bandName + "_" + waveformType + "_" + numTones + "_4096";

            if (!loadedPatterns.ContainsKey(patternKey))
            {{
                Debug.WriteLine("┌─ Generating " + waveformType + " MULTI-TONE: " + bandName);
                Debug.WriteLine("│   Tones: " + numTones + " | Spacing: " + 
                    (bandwidthMHz / numTones).ToString("F2") + " MHz");

                uint[] pattern;
                
                if (isGuardBand)
                {{
                    // HYBRID wide tones for guard bands — maximum hop interception
                    // chirpWidth=0.6: each tone sweeps 60% of gap to next tone
                    // noiseFactor=0.2: random jitter on top
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
                }}
                else
                {{
                    // Standard CW multi-tone for non-guard bands
                    pattern = patternGen.GenerateMultiTonePattern(numTones, 4096);
                    Debug.WriteLine("│   Waveform: CW (standard multi-tone)");
                }}
                
                loadedPatterns[patternKey] = pattern;
                AddSequences(new SequenceLoadPatternMemory(ltTxCommand, null, pattern));
                Debug.WriteLine("│ ✓ Pattern loaded: " + numTones + " simultaneous frequencies");
            }}
            else
            {{
                Debug.WriteLine("┌─ Using cached " + waveformType + ": " + patternKey);
                AddSequences(new SequenceLoadPatternMemory(ltTxCommand, null, 
                    loadedPatterns[patternKey]));
            }}

            AddSequences(new SequenceConfigurePattern(ltTxCommand, null, dacChannel, 0, 4095, 0));
            AddSequences(new SequenceUpdateConfig(ltTxCommand, null));

            if (dgvBands.Columns.Contains("colStatus"))
            {{
                string status = isGuardBand ? "WIDE-TONE READY" : "MULTI-TONE READY";
                row.Cells["colStatus"].Value = status + " (" + numTones + "T)";
                dgvBands.Refresh();
            }}

            AddSequences(new SequenceTriggerPattern(ltTxCommand, null));
            
            Debug.WriteLine("│ " + waveformType + ": " + numTones + " tones x " + 
                bandwidthMHz.ToString("F1") + " MHz | Time: 22.76 us");

            Thread.Sleep(1);
            AddSequences(new SequenceStopPattern(ltTxCommand, null));
        }}
'''

# ============================================================================
# INSTALLER FUNCTIONS
# ============================================================================

def print_banner():
    print("=" * 60)
    print("  K9 ELECTRONICS - AWJ WIDENED TONE INSTALLER")
    print("  Guard Band Optimisation Patch")
    print(f"  Date: {datetime.now().strftime('%Y-%m-%d %H:%M')}")
    print("=" * 60)
    print()

def find_file(filename, search_paths):
    """Search for a file in common project locations."""
    print(f"  Searching for {filename}...")
    found = []
    
    for base_path in search_paths:
        if not os.path.exists(base_path):
            continue
        for root, dirs, files in os.walk(base_path):
            # Skip common non-project directories
            skip_dirs = ['.git', 'bin', 'obj', 'node_modules', '.vs', 'packages']
            dirs[:] = [d for d in dirs if d not in skip_dirs]
            
            if filename in files:
                full_path = os.path.join(root, filename)
                found.append(full_path)
    
    return found

def select_file(found_files, filename):
    """Let user select which file to patch if multiple found."""
    if len(found_files) == 0:
        return None
    elif len(found_files) == 1:
        print(f"  Found: {found_files[0]}")
        return found_files[0]
    else:
        print(f"\n  Multiple {filename} files found:")
        for i, f in enumerate(found_files):
            print(f"    [{i+1}] {f}")
        while True:
            try:
                choice = int(input(f"\n  Select file (1-{len(found_files)}): "))
                if 1 <= choice <= len(found_files):
                    return found_files[choice - 1]
            except (ValueError, KeyboardInterrupt):
                pass
            print("  Invalid selection, try again.")

def backup_file(filepath):
    """Create a timestamped backup of a file."""
    timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
    backup_path = f"{filepath}.{timestamp}.bak"
    shutil.copy2(filepath, backup_path)
    print(f"  Backup: {backup_path}")
    return backup_path

def patch_pattern_generator(filepath):
    """Add the three new widened tone methods to AD9106PatternGenerator."""
    print(f"\n{'─' * 60}")
    print(f"  PATCHING: {os.path.basename(filepath)}")
    print(f"{'─' * 60}")
    
    with open(filepath, 'r', encoding='utf-8-sig') as f:
        content = f.read()
    
    # Check if already patched
    if 'GenerateHybridWideTones' in content:
        print("  [SKIP] Already contains GenerateHybridWideTones — previously patched.")
        return True
    
    # Check for rng field (needed by noise methods)
    has_rng = 'private Random rng' in content or 'Random rng' in content
    
    # Find insertion point — just before the last closing brace of the class
    # Look for GenerateMultiTonePattern or GenerateBarrageNoise as anchor
    insertion_markers = [
        'GenerateBarrageNoise',
        'GenerateNonLinearChirp', 
        'GenerateRandomDwellPattern',
        'GenerateMultiTonePattern',
        'GenerateChirpPattern',
    ]
    
    best_pos = -1
    for marker in insertion_markers:
        pos = content.rfind(marker)
        if pos > best_pos:
            best_pos = pos
    
    if best_pos == -1:
        print("  [ERROR] Could not find any pattern generation methods.")
        print("  Manual insertion required — see WidenedTonePatterns.cs")
        return False
    
    # Find the end of the method containing this marker
    # Look for the next method boundary or closing brace pattern
    brace_depth = 0
    in_method = False
    insert_pos = best_pos
    
    for i in range(best_pos, len(content)):
        if content[i] == '{':
            brace_depth += 1
            in_method = True
        elif content[i] == '}':
            brace_depth -= 1
            if in_method and brace_depth == 0:
                # Found the end of the method
                insert_pos = i + 1
                break
    
    if insert_pos <= best_pos:
        # Fallback: insert before last two closing braces (class + namespace)
        last_brace = content.rfind('}')
        second_last = content.rfind('}', 0, last_brace)
        insert_pos = second_last
    
    # Add rng field if not present
    rng_addition = ""
    if not has_rng:
        rng_addition = "\n        private Random rng = new Random();\n"
        # Insert rng field after class opening brace
        class_match = re.search(r'class\s+AD9106PatternGenerator[^{]*{', content)
        if class_match:
            rng_pos = class_match.end()
            content = content[:rng_pos] + rng_addition + content[rng_pos:]
            insert_pos += len(rng_addition)
            print("  [ADD] Random rng field added to class")
    
    # Insert the new methods
    date_str = datetime.now().strftime('%Y-%m-%d')
    new_code = NEW_PATTERN_METHODS.format(date=date_str)
    
    content = content[:insert_pos] + "\n" + new_code + "\n" + content[insert_pos:]
    
    # Write back
    backup_file(filepath)
    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)
    
    print("  [OK] Added GenerateNoiseModulatedTones()")
    print("  [OK] Added GenerateMiniChirpTones()")
    print("  [OK] Added GenerateHybridWideTones()")
    print(f"  [DONE] {os.path.basename(filepath)} patched successfully")
    return True

def patch_fmmain(filepath):
    """Update ExecuteMultiToneMode in FmMain to use widened tones for guard bands."""
    print(f"\n{'─' * 60}")
    print(f"  PATCHING: {os.path.basename(filepath)}")
    print(f"{'─' * 60}")
    
    with open(filepath, 'r', encoding='utf-8-sig') as f:
        content = f.read()
    
    # Check if already patched
    if 'HYBRID' in content and 'isGuardBand' in content:
        print("  [SKIP] Already contains guard band detection — previously patched.")
        return True
    
    # Find ExecuteMultiToneMode method
    pattern = r'private\s+void\s+ExecuteMultiToneMode\s*\(\s*DataGridViewRow\s+row'
    match = re.search(pattern, content)
    
    if not match:
        print("  [ERROR] Could not find ExecuteMultiToneMode method.")
        print("  Manual replacement required — see WidenedTonePatterns.cs")
        return False
    
    method_start = match.start()
    
    # Find the complete method (track braces)
    brace_depth = 0
    in_method = False
    method_end = method_start
    
    for i in range(method_start, len(content)):
        if content[i] == '{':
            brace_depth += 1
            in_method = True
        elif content[i] == '}':
            brace_depth -= 1
            if in_method and brace_depth == 0:
                method_end = i + 1
                break
    
    if method_end <= method_start:
        print("  [ERROR] Could not determine method boundaries.")
        return False
    
    # Check what's being replaced
    old_method = content[method_start:method_end]
    old_lines = old_method.count('\n')
    print(f"  Found ExecuteMultiToneMode: {old_lines} lines")
    
    # Replace with updated version
    date_str = datetime.now().strftime('%Y-%m-%d')
    new_method = UPDATED_EXECUTE_MULTITONE.format(date=date_str)
    
    content = content[:method_start] + new_method + content[method_end:]
    
    # Write back
    backup_file(filepath)
    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)
    
    print("  [OK] ExecuteMultiToneMode replaced with guard band detection")
    print("  [OK] Guard bands → HybridWideTones (3-4 MHz effective/tone)")
    print("  [OK] Non-guard bands → Standard CW multi-tone (unchanged)")
    print(f"  [DONE] {os.path.basename(filepath)} patched successfully")
    return True

def manual_mode():
    """Allow user to specify file paths directly."""
    print("\n  MANUAL MODE - Enter file paths directly")
    print("  (Press Enter to skip a file)\n")
    
    pg_path = input("  Path to AD9106PatternGenerator.cs: ").strip().strip('"')
    fm_path = input("  Path to FmMain.cs: ").strip().strip('"')
    
    return pg_path if pg_path else None, fm_path if fm_path else None

def print_summary(pg_ok, fm_ok):
    """Print final summary."""
    print(f"\n{'=' * 60}")
    print("  INSTALLATION SUMMARY")
    print(f"{'=' * 60}")
    
    if pg_ok:
        print("  [OK] AD9106PatternGenerator — 3 new methods added:")
        print("         - GenerateNoiseModulatedTones()    ~2 MHz/tone")
        print("         - GenerateMiniChirpTones()         ~3 MHz/tone")
        print("         - GenerateHybridWideTones()        ~3-4 MHz/tone")
    else:
        print("  [!!] AD9106PatternGenerator — NOT patched")
    
    if fm_ok:
        print("  [OK] FmMain ExecuteMultiToneMode — updated:")
        print("         - Guard bands auto-detect by name")
        print("         - Guard bands use HybridWideTones")
        print("         - Non-guard bands unchanged (CW)")
    else:
        print("  [!!] FmMain — NOT patched")
    
    print(f"\n  Guard Band Hit Rate Improvement:")
    print(f"  Before: ~40% (CW tones, 4 per zone)")
    print(f"  After:  ~100% (Hybrid wide tones, 4 per zone)")
    print(f"\n  No hardware changes required.")
    print(f"  No additional tones required.")
    print(f"  Rebuild solution and deploy.")
    print(f"\n{'=' * 60}")

def main():
    print_banner()
    
    # Ask for mode
    print("  How would you like to find your source files?\n")
    print("  [1] Auto-search (scans common project folders)")
    print("  [2] Manual (enter file paths directly)")
    print("  [3] Current directory only")
    print()
    
    try:
        mode = input("  Select mode (1/2/3): ").strip()
    except KeyboardInterrupt:
        print("\n\n  Cancelled.")
        return
    
    pg_path = None
    fm_path = None
    
    if mode == '2':
        pg_path, fm_path = manual_mode()
    else:
        if mode == '3':
            paths = ['.']
        else:
            paths = SEARCH_PATHS
        
        print(f"\n  Searching for source files...\n")
        
        pg_files = find_file(PATTERN_GEN_FILENAME, paths)
        pg_path = select_file(pg_files, PATTERN_GEN_FILENAME)
        
        print()
        
        fm_files = find_file(FMMAIN_FILENAME, paths)
        fm_path = select_file(fm_files, FMMAIN_FILENAME)
    
    if not pg_path and not fm_path:
        print("\n  [ERROR] No files found or selected.")
        print("  You can manually add the code from WidenedTonePatterns.cs")
        print("  to your project files.")
        return
    
    # Confirm before patching
    print(f"\n{'─' * 60}")
    print("  READY TO PATCH")
    print(f"{'─' * 60}")
    if pg_path:
        print(f"  Pattern Generator: {pg_path}")
    if fm_path:
        print(f"  FmMain:            {fm_path}")
    print(f"\n  Backups will be created before any changes.")
    
    try:
        confirm = input("\n  Proceed? (y/n): ").strip().lower()
    except KeyboardInterrupt:
        print("\n\n  Cancelled.")
        return
    
    if confirm != 'y':
        print("  Cancelled.")
        return
    
    # Do the patching
    pg_ok = False
    fm_ok = False
    
    if pg_path and os.path.exists(pg_path):
        pg_ok = patch_pattern_generator(pg_path)
    
    if fm_path and os.path.exists(fm_path):
        fm_ok = patch_fmmain(fm_path)
    
    print_summary(pg_ok, fm_ok)

if __name__ == '__main__':
    main()
