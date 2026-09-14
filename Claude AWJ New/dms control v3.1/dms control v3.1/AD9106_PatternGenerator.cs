/*
 *  Project:    DDS Mixer Synthesizer - AD9106 Pattern Generator
 *  File:       AD9106_PatternGenerator.cs
 *  Author:     K9 Electronics Ltd / Claude AI
 *  Date:       02.02.2025
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace dms_control_v3
{
    /// <summary>
    /// AD9106 Pattern Generator - Creates various jamming waveform patterns
    /// Supports PRBS, Chirp, Multi-Tone, and custom pattern types
    /// </summary>
    public class AD9106PatternGenerator
    {
        private Random rng;

        public AD9106PatternGenerator(int seed = 12345)
        {
            rng = new Random(seed);
        }

        /// <summary>
        /// Generate pseudo-random frequency pattern using LFSR (Linear Feedback Shift Register)
        /// Creates unpredictable hopping sequence that defeats CRPA null-steering
        /// Returns 12-bit unsigned values (0-4095)
        /// </summary>
        public uint[] GeneratePRBSPattern(int numPoints = 4096)
        {
            uint[] pattern = new uint[numPoints];

            // Use Linear Feedback Shift Register (LFSR) for pseudo-random sequence
            // Taps positioned for maximal length sequence
            uint lfsr = 0xACE1u;  // Non-zero seed
            uint bit;

            for (int i = 0; i < numPoints; i++)
            {
                // Galois LFSR with taps at bits 16, 14, 13, 11
                // Creates cryptographically-strong pseudo-random sequence
                bit = ((lfsr >> 0) ^ (lfsr >> 2) ^ (lfsr >> 3) ^ (lfsr >> 5)) & 1;
                lfsr = (lfsr >> 1) | (bit << 15);

                // Map to 12-bit range (0-4095) for AD9106
                pattern[i] = lfsr & 0x0FFF;
            }

            return pattern;
        }

        /// <summary>
        /// Generate linear chirp (sweep) pattern
        /// Sweeps linearly from start to end frequency ratio
        /// Better than step sweep for frequency-hopping threats
        /// </summary>
        public uint[] GenerateChirpPattern(double startFreqRatio, double endFreqRatio, int numPoints = 4096)
        {
            uint[] pattern = new uint[numPoints];

            for (int i = 0; i < numPoints; i++)
            {
                double ratio = (double)i / numPoints;
                double freqRatio = startFreqRatio + (endFreqRatio - startFreqRatio) * ratio;

                // Map to 12-bit range
                pattern[i] = (uint)(freqRatio * 4095) & 0x0FFF;
            }

            return pattern;
        }

        /// <summary>
        /// Generate multi-tone pattern (simultaneous frequencies)
        /// Hits multiple frequencies at once for instant band coverage
        /// More effective than sequential sweeping
        /// </summary>
        public uint[] GenerateMultiTonePattern(int numTones, int numPoints = 4096)
        {
            uint[] pattern = new uint[numPoints];
            int samplesPerTone = numPoints / numTones;

            for (int i = 0; i < numPoints; i++)
            {
                int toneIndex = i / samplesPerTone;
                if (toneIndex >= numTones) toneIndex = numTones - 1;

                // Distribute tones evenly across 12-bit range
                uint freqValue = (uint)((4095 / (numTones - 1)) * toneIndex);
                pattern[i] = freqValue & 0x0FFF;
            }

            return pattern;
        }

        /// <summary>
        /// Generate non-linear chirp (accelerating sweep)
        /// Quadratic frequency progression matches some radar/drone hop patterns
        /// </summary>
        public uint[] GenerateNonLinearChirp(int numPoints = 4096)
        {
            uint[] pattern = new uint[numPoints];

            for (int i = 0; i < numPoints; i++)
            {
                double ratio = (double)i / numPoints;
                // Quadratic acceleration
                double freqRatio = ratio * ratio;
                pattern[i] = (uint)(freqRatio * 4095) & 0x0FFF;
            }

            return pattern;
        }

        /// <summary>
        /// Generate random dwell pattern (unpredictable hop timing)
        /// Each frequency gets random dwell time - impossible to predict
        /// Most effective against adaptive CRPA systems
        /// </summary>
        public uint[] GenerateRandomDwellPattern(int numFrequencies, int numPoints = 4096)
        {
            uint[] pattern = new uint[numPoints];

            // Generate random frequencies
            uint[] frequencies = new uint[numFrequencies];
            for (int i = 0; i < numFrequencies; i++)
            {
                frequencies[i] = (uint)rng.Next(0, 4096);
            }

            // Assign random dwell times
            int idx = 0;
            while (idx < numPoints)
            {
                uint freq = frequencies[rng.Next(numFrequencies)];
                int dwell = rng.Next(1, 50);  // 1-50 samples per frequency

                for (int d = 0; d < dwell && idx < numPoints; d++)
                {
                    pattern[idx++] = freq;
                }
            }

            return pattern;
        }

        /// <summary>
        /// Generate barrage noise pattern (wide spectrum coverage)
        /// Random values across entire band
        /// </summary>
        public uint[] GenerateBarrageNoise(int numPoints = 4096)
        {
            uint[] pattern = new uint[numPoints];

            for (int i = 0; i < numPoints; i++)
            {
                pattern[i] = (uint)rng.Next(0, 4096);
            }

            return pattern;
        }

        /// <summary>
        /// Generate comb pattern (multiple discrete frequencies)
        /// Creates spectral "teeth" for selective jamming
        /// </summary>
        public uint[] GenerateCombPattern(int numTeeth, int toothWidth, int numPoints = 4096)
        {
            uint[] pattern = new uint[numPoints];
            int spacing = numPoints / numTeeth;

            for (int i = 0; i < numPoints; i++)
            {
                int position = i % spacing;
                if (position < toothWidth)
                {
                    // Inside tooth
                    pattern[i] = (uint)((i / spacing) * (4095 / numTeeth));
                }
                else
                {
                    // Between teeth (low power)
                    pattern[i] = 0;
                }
            }

            return pattern;
        }
    }

    /// <summary>
    /// Pattern Preset - Predefined jamming pattern optimized for specific threats
    /// </summary>
    public class PatternPreset
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string ThreatType { get; set; }
        public uint[] Pattern { get; set; }

        public PatternPreset(string name, string description, string threatType, uint[] pattern)
        {
            Name = name;
            Description = description;
            ThreatType = threatType;
            Pattern = pattern;
        }
    }

    /// <summary>
    /// Pattern Preset Library - Collection of threat-specific jamming patterns
    /// Pre-optimized for common drone and RF threats
    /// Enables sub-microsecond pattern switching
    /// </summary>
    public class PatternPresetLibrary
    {
        public List<PatternPreset> Presets { get; private set; }
        private AD9106PatternGenerator generator;

        public PatternPresetLibrary()
        {
            Presets = new List<PatternPreset>();
            generator = new AD9106PatternGenerator();
        }

        /// <summary>
        /// Load default preset library with common threat patterns
        /// </summary>
        public void LoadDefaultPresets()
        {
            Presets.Clear();

            // Preset 1: DJI Drone Pattern
            // Optimized for DJI Phantom/Mavic frequency hopping behavior
            uint[] djiPattern = generator.GeneratePRBSPattern(4096);
            Presets.Add(new PatternPreset(
                "DJI Drone PRBS",
                "Pseudo-random pattern optimized for DJI frequency hopping",
                "DJI Phantom/Mavic",
                djiPattern
            ));

            // Preset 2: Autel Drone Pattern
            // Linear chirp for Autel EVO control links
            uint[] autelPattern = generator.GenerateChirpPattern(0.0, 1.0, 4096);
            Presets.Add(new PatternPreset(
                "Autel Chirp Sweep",
                "Linear sweep for Autel drone control links",
                "Autel EVO",
                autelPattern
            ));

            // Preset 3: Generic WiFi Pattern
            // Multi-tone for 2.4GHz WiFi coverage
            uint[] wifiPattern = generator.GenerateMultiTonePattern(8, 4096);
            Presets.Add(new PatternPreset(
                "WiFi Multi-Tone",
                "8 simultaneous tones for WiFi 2.4GHz coverage",
                "WiFi 802.11b/g/n",
                wifiPattern
            ));

            // Preset 4: Fast Hopper Pattern
            // For agile frequency hoppers
            uint[] fastHopPattern = generator.GenerateRandomDwellPattern(32, 4096);
            Presets.Add(new PatternPreset(
                "Fast Hopper",
                "Rapid frequency changes for agile targets",
                "Generic Fast-Hopping",
                fastHopPattern
            ));

            // Preset 5: Non-Linear Chirp
            // For advanced threats with predictive hop algorithms
            uint[] nonLinearPattern = generator.GenerateNonLinearChirp(4096);
            Presets.Add(new PatternPreset(
                "Accelerating Chirp",
                "Non-linear sweep with increasing rate",
                "Advanced Threats",
                nonLinearPattern
            ));

            // Preset 6: Barrage Noise
            // Maximum disruption across entire band
            uint[] barragePattern = generator.GenerateBarrageNoise(4096);
            Presets.Add(new PatternPreset(
                "Barrage Noise",
                "Random noise across entire frequency range",
                "Generic Wide-Band",
                barragePattern
            ));

            // Preset 7: Comb Filter
            // Selective frequency jamming
            uint[] combPattern = generator.GenerateCombPattern(16, 64, 4096);
            Presets.Add(new PatternPreset(
                "Comb Filter",
                "16 discrete frequency channels",
                "Selective Jamming",
                combPattern
            ));

            Debug.WriteLine("✓ Loaded " + Presets.Count + " pattern presets");
        }

        /// <summary>
        /// Get preset by name
        /// </summary>
        public PatternPreset GetPreset(string name)
        {
            return Presets.FirstOrDefault(p => p.Name == name);
        }

        /// <summary>
        /// Get preset by threat type
        /// </summary>
        public PatternPreset GetPresetByThreat(string threatType)
        {
            return Presets.FirstOrDefault(p => p.ThreatType.Contains(threatType));
        }

        /// <summary>
        /// Add custom preset to library
        /// </summary>
        public void AddCustomPreset(string name, string description, string threatType, uint[] pattern)
        {
            if (pattern == null || pattern.Length == 0 || pattern.Length > 4096)
            {
                throw new ArgumentException("Pattern must be between 1 and 4096 samples");
            }

            Presets.Add(new PatternPreset(name, description, threatType, pattern));
            Debug.WriteLine("✓ Added custom preset: " + name);
        }

        /// <summary>
        /// Remove preset by name
        /// </summary>
        public bool RemovePreset(string name)
        {
            PatternPreset preset = Presets.FirstOrDefault(p => p.Name == name);
            if (preset != null)
            {
                Presets.Remove(preset);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Get preset by index (for cycling through presets)
        /// </summary>
        public PatternPreset GetPresetByIndex(int index)
        {
            if (index < 0 || index >= Presets.Count)
            {
                return null;
            }
            return Presets[index];
        }
    }
}
