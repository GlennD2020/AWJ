/*
 *  Project:    DDS Mixer Synthesizer - AD9106 Pattern Memory Support
 *  File:       AD9106_PatternMemorySequences.cs
 *  Author:     K9 Electronics Ltd / Claude AI
 *  Date:       02.02.2025
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace nsAlexKir
{
    using Sequences;

    /// <summary>
    /// Loads pattern data into AD9106 SRAM (addresses 0x6000-0x6FFF)
    /// Based on AD9106 datasheet pattern memory specifications
    /// </summary>
    public class SequenceLoadPatternMemory : CBaseSequence
    {
        List<Command> ltCmd;
        uint[] patternData;

        public SequenceLoadPatternMemory(List<Command> _ltCmd, dResultCallbackFunction _result_function, uint[] _patternData)
            : base(_result_function)
        {
            ltCmd = _ltCmd;
            patternData = _patternData;

            if (patternData == null || patternData.Length == 0)
            {
                throw new ArgumentException("Pattern data cannot be null or empty");
            }

            if (patternData.Length > 4096)
            {
                throw new ArgumentException("Pattern too long! Maximum 4096 samples.");
            }
        }

        public override StatusSequence SequenceFunc(StatusSequence status, params object[] data)
        {
            if (status == StatusSequence.eStart)
            {
                // STEP 1: Enable SRAM write access
                // PAT_STATUS (0x1E) = 0x0004
                // Bit 2 (MEM_ACCESS) = 1, Bit 3 (BUF_READ) = 0, Bit 0 (RUN) = 0
                Command cmdEnable = new Command(eTYPE_COMMANDS.WRITE_AD9106_REGISTER);
                cmdEnable.LenData = 4;
                cmdEnable.setUShort(0x1E, 0);   // Register address
                cmdEnable.setUShort(0x0004, 2); // Register value
                lock (ltCmd)
                {
                    ltCmd.Add(cmdEnable);
                }

                // STEP 2: Write pattern data to SRAM
                // SRAM addresses: 0x6000 to 0x6FFF
                // Data format: 12-bit left-justified (bits 15-4), bits 3-0 reserved
                for (int i = 0; i < patternData.Length; i++)
                {
                    ushort sramAddress = (ushort)(0x6000 + i);
                    
                    // Left-justify 12-bit data: shift by 4 bits
                    ushort sramData = (ushort)((patternData[i] & 0x0FFF) << 4);

                    Command cmdData = new Command(eTYPE_COMMANDS.WRITE_AD9106_REGISTER);
                    cmdData.LenData = 4;
                    cmdData.setUShort(sramAddress, 0);
                    cmdData.setUShort(sramData, 2);
                    lock (ltCmd)
                    {
                        ltCmd.Add(cmdData);
                    }
                }

                // STEP 3: Disable SRAM write access
                // PAT_STATUS (0x1E) = 0x0000
                Command cmdDisable = new Command(eTYPE_COMMANDS.WRITE_AD9106_REGISTER);
                cmdDisable.LenData = 4;
                cmdDisable.setUShort(0x1E, 0);   // Register address
                cmdDisable.setUShort(0x0000, 2); // Register value
                lock (ltCmd)
                {
                    ltCmd.Add(cmdDisable);
                }
            }
            else if (status == StatusSequence.eContinue)
            {
                return base.SequenceFunc(StatusSequence.eFinish, data);
            }
            return StatusSequence.eContinue;
        }
    }

    /// <summary>
    /// Configures AD9106 pattern playback from SRAM for specified DAC channel
    /// </summary>
    public class SequenceConfigurePattern : CBaseSequence
    {
        List<Command> ltCmd;
        int dacChannel;
        ushort startAddr;
        ushort stopAddr;
        ushort startDelay;

        public SequenceConfigurePattern(List<Command> _ltCmd, dResultCallbackFunction _result_function,
            int _dacChannel, ushort _startAddr, ushort _stopAddr, ushort _startDelay)
            : base(_result_function)
        {
            ltCmd = _ltCmd;
            dacChannel = _dacChannel;
            startAddr = _startAddr;
            stopAddr = _stopAddr;
            startDelay = _startDelay;

            if (dacChannel < 1 || dacChannel > 4)
            {
                throw new ArgumentException("DAC channel must be 1-4");
            }
        }

        public override StatusSequence SequenceFunc(StatusSequence status, params object[] data)
        {
            if (status == StatusSequence.eStart)
            {
                // Register addresses for each DAC channel
                ushort startDelayReg = 0;
                ushort startAddrReg = 0;
                ushort stopAddrReg = 0;
                ushort wavConfigReg = 0;

                switch (dacChannel)
                {
                    case 1:
                        startDelayReg = 0x5C;  // START_DLY1
                        startAddrReg = 0x5D;   // START_ADDR1
                        stopAddrReg = 0x5E;    // STOP_ADDR1
                        wavConfigReg = 0x27;   // WAV2_1CONFIG
                        break;
                    case 2:
                        startDelayReg = 0x58;  // START_DLY2
                        startAddrReg = 0x59;   // START_ADDR2
                        stopAddrReg = 0x5A;    // STOP_ADDR2
                        wavConfigReg = 0x27;   // WAV2_1CONFIG
                        break;
                    case 3:
                        startDelayReg = 0x54;  // START_DLY3
                        startAddrReg = 0x55;   // START_ADDR3
                        stopAddrReg = 0x56;    // STOP_ADDR3
                        wavConfigReg = 0x26;   // WAV4_3CONFIG
                        break;
                    case 4:
                        startDelayReg = 0x50;  // START_DLY4
                        startAddrReg = 0x51;   // START_ADDR4
                        stopAddrReg = 0x52;    // STOP_ADDR4
                        wavConfigReg = 0x26;   // WAV4_3CONFIG
                        break;
                }

                // Set start delay (16-bit register)
                Command cmdDelay = new Command(eTYPE_COMMANDS.WRITE_AD9106_REGISTER);
                cmdDelay.LenData = 4;
                cmdDelay.setUShort(startDelayReg, 0);
                cmdDelay.setUShort(startDelay, 2);
                lock (ltCmd)
                {
                    ltCmd.Add(cmdDelay);
                }

                // Set start address (12-bit, left-justified in upper 12 bits)
                ushort startAddrData = (ushort)((startAddr & 0x0FFF) << 4);
                Command cmdStartAddr = new Command(eTYPE_COMMANDS.WRITE_AD9106_REGISTER);
                cmdStartAddr.LenData = 4;
                cmdStartAddr.setUShort(startAddrReg, 0);
                cmdStartAddr.setUShort(startAddrData, 2);
                lock (ltCmd)
                {
                    ltCmd.Add(cmdStartAddr);
                }

                // Set stop address (12-bit, left-justified in upper 12 bits)
                ushort stopAddrData = (ushort)((stopAddr & 0x0FFF) << 4);
                Command cmdStopAddr = new Command(eTYPE_COMMANDS.WRITE_AD9106_REGISTER);
                cmdStopAddr.LenData = 4;
                cmdStopAddr.setUShort(stopAddrReg, 0);
                cmdStopAddr.setUShort(stopAddrData, 2);
                lock (ltCmd)
                {
                    ltCmd.Add(cmdStopAddr);
                }

                // Set waveform select to SRAM (WAVE_SELx = 0x0)
                Command cmdWavConfig = new Command(eTYPE_COMMANDS.WRITE_AD9106_REGISTER);
                cmdWavConfig.LenData = 4;
                cmdWavConfig.setUShort(wavConfigReg, 0);
                cmdWavConfig.setUShort(0x0000, 2);  // WAVE_SEL = 0 for SRAM
                lock (ltCmd)
                {
                    ltCmd.Add(cmdWavConfig);
                }
            }
            else if (status == StatusSequence.eContinue)
            {
                return base.SequenceFunc(StatusSequence.eFinish, data);
            }
            return StatusSequence.eContinue;
        }
    }

    /// <summary>
    /// Triggers AD9106 pattern playback by setting RUN bit
    /// </summary>
    public class SequenceTriggerPattern : CBaseSequence
    {
        List<Command> ltCmd;

        public SequenceTriggerPattern(List<Command> _ltCmd, dResultCallbackFunction _result_function)
            : base(_result_function)
        {
            ltCmd = _ltCmd;
        }

        public override StatusSequence SequenceFunc(StatusSequence status, params object[] data)
        {
            if (status == StatusSequence.eStart)
            {
                // Set RUN bit in PAT_STATUS register (0x1E, bit 0)
                // This arms the pattern generator
                // Actual trigger comes from TRIGGER pin going low
                Command cmd = new Command(eTYPE_COMMANDS.WRITE_AD9106_REGISTER);
                cmd.LenData = 4;
                cmd.setUShort(0x1E, 0);    // PAT_STATUS register
                cmd.setUShort(0x0001, 2);  // RUN = 1
                lock (ltCmd)
                {
                    ltCmd.Add(cmd);
                }
            }
            else if (status == StatusSequence.eContinue)
            {
                return base.SequenceFunc(StatusSequence.eFinish, data);
            }
            return StatusSequence.eContinue;
        }
    }

    /// <summary>
    /// Stops AD9106 pattern playback by clearing RUN bit
    /// </summary>
    public class SequenceStopPattern : CBaseSequence
    {
        List<Command> ltCmd;

        public SequenceStopPattern(List<Command> _ltCmd, dResultCallbackFunction _result_function)
            : base(_result_function)
        {
            ltCmd = _ltCmd;
        }

        public override StatusSequence SequenceFunc(StatusSequence status, params object[] data)
        {
            if (status == StatusSequence.eStart)
            {
                // Clear RUN bit in PAT_STATUS register (0x1E)
                Command cmd = new Command(eTYPE_COMMANDS.WRITE_AD9106_REGISTER);
                cmd.LenData = 4;
                cmd.setUShort(0x1E, 0);    // PAT_STATUS register
                cmd.setUShort(0x0000, 2);  // RUN = 0
                lock (ltCmd)
                {
                    ltCmd.Add(cmd);
                }
            }
            else if (status == StatusSequence.eContinue)
            {
                return base.SequenceFunc(StatusSequence.eFinish, data);
            }
            return StatusSequence.eContinue;
        }
    }

    /// <summary>
    /// Updates AD9106 configuration (transfers shadow registers to active)
    /// </summary>
    public class SequenceUpdateConfig : CBaseSequence
    {
        List<Command> ltCmd;

        public SequenceUpdateConfig(List<Command> _ltCmd, dResultCallbackFunction _result_function)
            : base(_result_function)
        {
            ltCmd = _ltCmd;
        }

        public override StatusSequence SequenceFunc(StatusSequence status, params object[] data)
        {
            if (status == StatusSequence.eStart)
            {
                // Set RAMUPDATE bit in register 0x1D
                // This transfers all shadow register settings to active registers
                // Bit is self-clearing
                Command cmd = new Command(eTYPE_COMMANDS.WRITE_AD9106_REGISTER);
                cmd.LenData = 4;
                cmd.setUShort(0x1D, 0);    // RAMUPDATE register
                cmd.setUShort(0x0001, 2);  // RAMUPDATE = 1 (self-clearing)
                lock (ltCmd)
                {
                    ltCmd.Add(cmd);
                }
            }
            else if (status == StatusSequence.eContinue)
            {
                return base.SequenceFunc(StatusSequence.eFinish, data);
            }
            return StatusSequence.eContinue;
        }
    }
}
