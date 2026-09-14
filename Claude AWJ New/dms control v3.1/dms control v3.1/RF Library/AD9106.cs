/*
 * File:        AD9106.cs
 * Description: Class for works with DDS.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace nsAlexKir.RF_Library
{
    public enum AD9106_MODE : ushort
    {
        CONTINUE=0,
        SOFTWARE=1,
        RAMP=2,
        PSEUDO=3,
    }

    /// <summary> Регистр синтезатора частот MAX2871. </summary>
    [Serializable]
    [XmlRoot("RegisterAD9106")]
    public class RegisterAD9106 : BaseRegister<ulong> 
    {
        /// <summary> Тело регистра. </summary>
        [XmlElement("body")]
        public override ulong body
        {
            get
            {
                return (ulong)(data[3] << 24) + (ulong)(data[2] << 16) + (ulong)(data[1] << 8) + (ulong)data[0];
            }
            set
            {
                data[0] = (byte)((value >> 0) & 0xFF);
                data[1] = (byte)((value >> 8) & 0xFF);
                data[2] = (byte)((value >> 16) & 0xFF);
                data[3] = (byte)((value >> 24) & 0xFF);
            }
        }
        /// <summary> Адрес регистра. </summary>
        [XmlIgnore]
        public override int address
        {
            get
            {
                return ((int)(data[3]*256) + (int)data[2]);
            }
            set
            {
                data[2] = (byte)(value & 0xFF);
                data[3] = (byte)((value>>8) & 0xFF);
            }
        }

        /// <summary> Конструктор класса  </summary>
        public RegisterAD9106 ( )
            : base(4)
        {
            body = 0;
        }
        /// <summary> Конструктор класса. </summary>
        public RegisterAD9106 ( ulong _body )
            : base(4)
        {
            body = _body;
        }
    }

    /// <summary> Настройка DDS. </summary>
    [Serializable]
    public class AD9106
    {
        public const ushort DEFAULT_DIGITAL_GAIN = 128;

        /// <summary> Полоса перестройки </summary>
        private double      RampBandwidth;

        /// <summary> Массив регистров. </summary>
        [XmlArray("registers")]
        public ushort []    registers;

        /// <summary> Управление программной перестройкой. </summary>
        [XmlElement("SweepOn")]
        public ushort       SweepOn;

        /// <summary> Begin frequency </summary>
        [XmlIgnore]
        public float        Start 
        {
            set
            {
                double  dFTW = (value / RefClock) * 16777216.0;
                ulong   ulFTW = (ulong)dFTW;

                registers[0x3E] = (ushort)((ulFTW & 0xFFFF00) >> 8);
                registers[0x3F] = (ushort)((ulFTW & 0xFF) << 8);
            }
            get
            {
                ulong ulFTW = (ulong)((registers[0x3F] & 0xFF00)>>8);
                ulFTW = ulFTW + (ulong)(registers[0x3E] << 8);

                float dFreq = (float)ulFTW;
                dFreq = (float)((dFreq / 16777216.0F) * RefClock);
                return dFreq;
            }
        }

        /// <summary> Frequency step </summary>
        [XmlElement("Step")]
        public float        Step;
         
        /// <summary> Number points of sweep </summary>
        [XmlElement("Points")]
        public ulong        Points;

        /// <summary> Frequency tuning word </summary>
        [XmlIgnore]
        public ulong        FTW 
        {
            set
            {
                registers[0x3E] = (ushort)((value & 0xFFFF00) >> 8);
                registers[0x3F] = (ushort)((value & 0xFF) << 8);
            }
            get
            {
                ulong ulFTW = (ulong)((registers[0x3F] & 0xFF00)>>8);
                ulFTW = ulFTW + (ulong)(registers[0x3E] << 8);
                return ulFTW;
            }
        }

        /// <summary> Максимальное значение линейное перестройки. </summary>
        [XmlElement("MaxRamp")]
        public ushort       MaxRamp;

        /// <summary> Временой интервал. </summary>
        [XmlElement("Interval")]
        public double       Interval;

        /// <summary> Опорная частота. </summary>
        [XmlElement("RefClock")]
        public double       RefClock;

        /// <summary> Частота управления. </summary>
        [XmlElement("FreqCtrl")]
        public float        FreqCtrl;

        /// <summary> Pattern Period. </summary>
        [XmlIgnore]
        public ushort       PatternPeriod 
        {
            set
            {
                this.registers[0x29] = value;
            }
            get
            {
                return this.registers[0x29];
            }
        }

        /// <summary> Используемые каналы </summary>
        [XmlIgnore]
        public bool []      Channels;

        /// <summary> Массив коэффициентов усилений для каналов. </summary>
        [XmlIgnore]
        public ushort []    Gain;

        /// <summary> Режим работы микросхемы DDS. </summary>
        public AD9106_MODE  getMode         ( ) 
        {
            if ( this.Channels[0] ) 
            {
                switch ( this.registers[0x27] & 0x00FF ) 
                {
                    case 0x0021: return AD9106_MODE.PSEUDO;
                    case 0x0033: return AD9106_MODE.CONTINUE;
                    case 0x0031: return AD9106_MODE.RAMP;
                }
            }
            else if ( this.Channels[1] )
            {
                switch ( this.registers[0x27] & 0xFF00 )
                {
                    case 0x2100: return AD9106_MODE.PSEUDO;
                    case 0x3300: return AD9106_MODE.CONTINUE;
                    case 0x3100: return AD9106_MODE.RAMP;
                }
            }
            else if ( this.Channels[2] )
            {
                switch ( this.registers[0x26] & 0x00FF ) 
                {
                    case 0x0021: return AD9106_MODE.PSEUDO;
                    case 0x0033: return AD9106_MODE.CONTINUE;
                    case 0x0031: return AD9106_MODE.RAMP;
                }
            }
            else if ( this.Channels[3] ) 
            {
                switch ( this.registers[0x26] & 0xFF00 ) 
                {
                    case 0x2100: return AD9106_MODE.PSEUDO;
                    case 0x3300: return AD9106_MODE.CONTINUE;
                    case 0x3100: return AD9106_MODE.RAMP;
                }
            }

            return AD9106_MODE.CONTINUE;
        }

        /// <summary> Set the mode of the DDS </summary>
        /// <param name="mode">Mode of the works DDS</param>
        public void         setMode         ( AD9106_MODE mode ) 
        {
            if ( mode==AD9106_MODE.PSEUDO )
            {
                this.registers[0x26]  = (ushort)((this.Channels[3]) ? 0x2100 : 0x0000);
                this.registers[0x26] += (ushort)((this.Channels[2]) ? 0x0021 : 0x0000);
                this.registers[0x27]  = (ushort)((this.Channels[1]) ? 0x2100 : 0x0000);
                this.registers[0x27] += (ushort)((this.Channels[0]) ? 0x0021 : 0x0000);
            }
            else if ( mode==AD9106_MODE.CONTINUE || mode==AD9106_MODE.SOFTWARE )
            {
                this.registers[0x26]  = (ushort)((this.Channels[3]) ? 0x3300 : 0x0000);
                this.registers[0x26] += (ushort)((this.Channels[2]) ? 0x0033 : 0x0000);
                this.registers[0x27]  = (ushort)((this.Channels[1]) ? 0x3300 : 0x0000);
                this.registers[0x27] += (ushort)((this.Channels[0]) ? 0x0033 : 0x0000);
            }
            else if ( mode==AD9106_MODE.RAMP)
            {
                this.registers[0x26]  = (ushort)((this.Channels[3]) ? 0x3200 : 0x0000);
                this.registers[0x26] += (ushort)((this.Channels[2]) ? 0x0032 : 0x0000);
                this.registers[0x27]  = (ushort)((this.Channels[1]) ? 0x3200 : 0x0000);
                this.registers[0x27] += (ushort)((this.Channels[0]) ? 0x0032 : 0x0000);
            }
        }

        /// <summary></summary>
        /// <param name="mode"></param>
        public void setWaveformCycle        ( ushort Cycle ) 
        {            
            this.registers[(ushort)(0x53 + 4*3)] = (ushort)((this.Channels[3]) ? Cycle : 0x0000);
            this.registers[(ushort)(0x53 + 4*2)] = (ushort)((this.Channels[2]) ? Cycle : 0x0000);
            this.registers[(ushort)(0x53 + 4*1)] = (ushort)((this.Channels[1]) ? Cycle : 0x0000);
            this.registers[(ushort)(0x53 + 4*0)] = (ushort)((this.Channels[0]) ? Cycle : 0x0000);
        }

        /// <summary> Set start address to wave form </summary>
        /// <param name="index"> Index of the channel </param>
        public void setWaveformStartAddress ( ushort index ) 
        {
            this.registers[(ushort)(0x51 + 4*3)] = (ushort)(0x6002 + index);
            this.registers[(ushort)(0x51 + 4*2)] = (ushort)(0x6002 + index);
            this.registers[(ushort)(0x51 + 4*1)] = (ushort)(0x6002 + index);
            this.registers[(ushort)(0x51 + 4*0)] = (ushort)(0x6002 + index);
        }

        /// <summary> Инициализация </summary>
        public void     InitRegisters       ( ) 
        {
            if ( registers==null )
            {
                registers = new ushort[128];
            }
        }

        /// <summary> Инициализация регистров по умолчанию.  </summary>
        public void     DefaultRegisters    ( ) 
        {
            registers = new ushort [] {
	            0x0000,	//	0x00/0   -
	            0x0000,	//	0x01/1   -
	            0x0000,	//	0x02/2   - 
	            0x0000,	//	0x03/3   - 
	            0x4000,	//	0x04/4   -
	            0x4000,	//	0x05/5   -
	            0x4000, //	0x06/6   -
	            0x4000, //	0x07/7   -
	            0x0000, //	0x08/8   - 
	            0x800A, //	0x09/9   -
	            0x800A, //	0x0A/10  -
	            0x800A, //	0x0B/11  -
	            0x800A, //	0x0C/12  -
	            0x0000, //	0x0D/13  - 
	            0x0000, //	0x0E/14  - 
	            0x0000, //	0x0F/15  - 
	            0x0000, //	0x10/16  - 
	            0x0000, //	0x11/17  - 
	            0x0000, //	0x12/18  - 
	            0x0000, //	0x13/19  - 
	            0x0000, //	0x14/20  -
	            0x0000, //	0x15/21  -
	            0x0000, //	0x16/22  - 
	            0x0000, //	0x17/23  - 
	            0x0000, //	0x18/24  - 
	            0x0000, //	0x19/25  - 
	            0x0000, //	0x1A/26  - 
	            0x0000, //	0x1B/27  - 
	            0x0000, //	0x1C/28  - 
	            0x0000, //	0x1D/29  - 
	            0x0000, //	0x1E/30  - 
	            0x0000, //	0x1F/31  - 
	            0x000E, //	0x20/32  - Trigger Start to Real Pattern Delay Register.
	            0x0000, //	0x21/33  - 
	            0x0000, //	0x22/34  - 
	            0x0000, //	0x23/35  - 
	            0x0000, //	0x24/36  - 
	            0x0000, //	0x25/37  - 
	            0x3333, //	0x26/38  - Wave3/Wave4 Select Register.
	            0x3333, //	0x27/39  - Wave1/Wave2 Select Register.
	            0x0111, //	0x28/40  - DAC Time Control Register.
	            0x8000, //	0x29/41  - Pattern Period Register.
	            0x0101, //	0x2A/42  - DAC3,4_REPEAT_CYCLE.
	            0x0101, //	0x2B/43  - DAC1,2_REPEAT_CYCLE.
	            0x0003, //	0x2C/44  - Trigger Start to DOUT Signal Register.
	            0x0000, //	0x2D/45  - 
	            0x0000, //	0x2E/46  - 
	            0x0000, //	0x2F/47  - 
	            0x0000, //	0x30/48  - 
	            0x0000, //	0x31/49  - 
	            0x4000, //	0x32/50  - DAC4 Digital Gain Register.
	            0x4000, //	0x33/51  - DAC3 Digital Gain Register.
	            0x4000, //	0x34/52  - DAC2 Digital Gain Register.
	            0x4000, //	0x35/53  - DAC1 Digital Gain Register.
	            0x0000, //	0x36/54  - DAC3/DAC4 Sawtooth Configuration Register.
	            0x0000, //	0x37/55  - DAC1/DAC2 Sawtooth Configuration Register.
	            0x0000, //	0x38/56  - 
	            0x0000, //	0x39/57  - 
	            0x0000, //	0x3A/58  - 
	            0x0000, //	0x3B/59  - 
	            0x0000, //	0x3C/60  - 
	            0x0000, //	0x3D/61  - 
	            0x0181,	//	0x3E/62  - DDSTW_MSB
	            0x8200,	//	0x3F/63  - DDSTW_LSB
	            0x0000, //	0x40/64  - DDS4 Phase Offset Register.
	            0x0000, //	0x41/65  - DDS3 Phase Offset Register.
	            0x0000, //	0x42/66  - DDS2 Phase Offset Register.
	            0x0000, //	0x43/67  - DDS1 Phase Offset Register.
	            0x0000, //	0x44/68  - Pattern Control 1 Register.
	            0x0000, //	0x45/69  - Pattern Control 2 Register.
	            0x0000, //	0x46/70  - 
	            0x0000, //	0x47/71  - 
	            0x0000, //	0x48/72  - 
	            0x0000, //	0x49/73  - 
	            0x0000, //	0x4A/74  - 
	            0x0000, //	0x4B/75  - 
	            0x0000, //	0x4C/76  - 
	            0x0000, //	0x4D/77  - 
	            0x0000, //	0x4E/78  - 
	            0x0000, //	0x4F/79  - 
	            0x07D0, //	0x50/80  - Start Delay4 Register.
	            0x0000, //	0x51/81  - Start Address4 Register.
	            0x0000, //	0x52/82  - Stop Address4 Register.
	            0x0100, //	0x53/83  - DDS Cycle4 Register.
	            0x03E8, //	0x54/84  -
	            0x0000, //	0x55/85  -
	            0x0000, //	0x56/86  - 
	            0x0100, //	0x57/87  -
	            0x0BB8, //	0x58/88  - 
	            0x0000, //	0x59/89  - 
	            0x0000, //	0x5A/90  - 
	            0x0101, //	0x5B/91  -
	            0x0FA0, //	0x5C/92  - 
	            0x0000, //	0x5D/93  - 
	            0x0000, //	0x5E/94  - 
	            0x0100, //	0x5F/95  -
	            0x0000, //	0x60/96  -
	            0x0000, //	0x61/97  -
	            0x0000, //	0x62/98  -
	            0x0000, //	0x63/99  -
	            0x0000, //	0x64/100 -
	            0x0000, //	0x65/101 -
	            0x0000, //	0x66/102 - 
	            0x0000, //	0x67/103 - 
	            0x0000, //	0x68/104 - 
	            0x0000, //	0x69/105 - 
	            0x0000, //	0x6A/106 - 
	            0x0000, //	0x6B/107 - 
	            0x0000, //	0x6C/108 - 
	            0x0000, //	0x6D/109 - 
	            0x0000, //	0x6E/110 - 
	            0x0000, //	0x6F/111 - 
	            0x0000, //	0x70/112 -
	            0x0000, //	0x71/113 -
	            0x0000, //	0x72/114 -
	            0x0000, //	0x73/115 -
	            0x0000, //	0x74/116 -
	            0x0000, //	0x75/117 -
	            0x0000, //	0x76/118 - 
	            0x0000, //	0x77/119 - 
	            0x0000, //	0x78/120 - 
	            0x0000, //	0x79/121 - 
	            0x0000, //	0x7A/122 - 
	            0x0000, //	0x7B/123 - 
	            0x0000, //	0x7C/124 - 
	            0x0000, //	0x7D/125 - 
	            0x0000, //	0x7E/126 - 
	            0x0000, //	0x7F/127 -
            };
        }

        /// <summary> Считывание регистра для задания фазы ваходного сигнала. </summary>
        /// <param name="channel"> Номер канала. </param>
        /// <returns> Значение регистра. </returns>
        public ushort   getPTW              ( int channel ) 
        {
            ushort ptw = 0;
            if ( channel==0 )
            {
                ptw = registers[0x43];
            }
            else if ( channel==1 )
            {
                ptw = registers[0x42];
            }
            else if ( channel==2 )
            {
                ptw = registers[0x41];
            }
            else if ( channel==3 )
            {
                ptw = registers[0x40];
            }
            return ptw;
        }

        /// <summary> Установка настройки фазы канала. </summary>
        /// <param name="channel"> Номер канала. </param>
        /// <param name="ptw"> Настройка фазы. </param>
        public void     setPTW              ( int channel, ushort ptw ) 
        {
            if ( channel==0 )
            {
                registers[0x43] = ptw;
            }
            else if ( channel==1 )
            {
                registers[0x42] = ptw;
            }
            else if ( channel==2 )
            {
                registers[0x41] = ptw;
            }
            else if ( channel==3 )
            {
                registers[0x40] = ptw;
            }
        }

        /// <summary></summary>
        /// <param name="channel"></param>
        public void     setDigitalGain      ( int channel, ushort gain ) 
        {
            ushort usGain = (ushort)(gain&0x0FFF);

            usGain = (ushort)(usGain << 4);

            if (channel==0)
            {
                registers[0x35] = usGain;
            }
            else if (channel==1)
            {
                registers[0x34] = usGain;
            }
            else if (channel==2)
            {
                registers[0x33] = usGain;
            }
            else if (channel==3)
            {
                registers[0x32] = usGain;
            }
        }

        /// <summary></summary>
        /// <param name="channel"></param>
        public ushort   getDigitalGain      ( int channel ) 
        {
            ushort gain = 0;

            if (channel==0)
            {
                gain = (ushort)(registers[0x35]);
            }
            else if (channel==1)
            {
                gain = (ushort)(registers[0x34]);
            }
            else if (channel==2)
            {
                gain = (ushort)(registers[0x33]);
            }
            else if (channel==3)
            {
                gain = (ushort)(registers[0x32]);
            }

            return (ushort)(gain>>4);
        }

        /// <summary></summary>
        /// <param name="channel"></param>
        public void     setAnalogGain       ( int channel, ushort gain ) 
        {
            ushort usGain = (ushort)(gain & 0x7F);

            if (channel==0)
            {
                registers[0x07] = usGain;
            }
            else if (channel==1)
            {
                registers[0x06] = usGain;
            }
            else if (channel==2)
            {
                registers[0x05] = usGain;
            }
            else if (channel==3)
            {
                registers[0x04] = usGain;
            }
        }

        /// <summary> Set analog gain </summary>
        /// <param name="channel"> Number of the channel </param>
        public ushort   getAnalogGain       ( int channel ) 
        {
            ushort gain = 0;

            if (channel==0)
            {
                gain = (ushort)(registers[0x07] & 0x007F);
            }
            else if (channel==1)
            {
                gain = (ushort)(registers[0x06] & 0x007F);
            }
            else if (channel==2)
            {
                gain = (ushort)(registers[0x05] & 0x007F);
            }
            else if (channel==3)
            {
                gain = (ushort)(registers[0x04] & 0x007F);
            }

            return (ushort)(gain);
        }

        /// <summary> Получение FTW для конкретной частотной точки. </summary>
        /// <param name="index"> номер частотной точки </param>
        /// <returns> FTW </returns>
        public ulong    getFTWpoint         ( int index ) 
        {
            double dCurPoint = Start + ((double)index) * Step;
            double  dFTW = (dCurPoint / RefClock) * 16777216.0;
            return (ulong)dFTW; 
        }

        /// <summary></summary>
        /// <returns></returns>
        public ulong    getBeginRampFTW     ( int tw_mem ) 
        {
            ulong  FTW = (ulong)((Start / RefClock) * 16777216.0);
            switch (tw_mem)
            {
                case 0:
                    break;
                case 1:
                    FTW = FTW & 0x800000;
                    break;
                case 2:
                    FTW = FTW & 0xC00000;
                    break;
                case 3:
                    FTW = FTW & 0xE00000;
                    break;
                case 4:
                    FTW = FTW & 0xF00000;
                    break;
                case 5:
                    FTW = FTW & 0xF80000;
                    break;
                case 6:
                    FTW = FTW & 0xFC0000;
                    break;
                case 7:
                    FTW = FTW & 0xFE0000;
                    break;
                case 8:
                    FTW = FTW & 0xFF0000;
                    break;
                case 9:
                    FTW = FTW & 0xFF8000;
                    break;
                case 10:
                    FTW = FTW & 0xFFC000;
                    break;
                case 11:
                    FTW = FTW & 0xFFE000;
                    break;
                case 12:
                    FTW = FTW & 0xFFF000;
                    break;
                case 13:
                    FTW = FTW & 0xFFF800;
                    break;
                case 14:
                    FTW = FTW & 0xFFFC00;
                    break;
                case 15:
                    FTW = FTW & 0xFFFE00;
                    break;
                case 16:
                    FTW = FTW & 0xFFFF00;
                    break;
            }
            FTW = FTW & 0x00000FFF;
            return FTW;
        }

        /// <summary> Функция расчета значения FTW для линейной перестройки. </summary>
        /// <param name="tw_mem"> Режим задания шага. </param>
        /// <param name="index"> Номер частоты </param>
        /// <returns> Расчитанное значение. </returns>
        public ulong    getRampFTW          ( int tw_mem, int index ) 
        {
            //double dCurPoint = ((double)index) * Step;
            double dCurPoint = Start + ((double)index) * Step;
            ulong  FTW = (ulong)((dCurPoint / RefClock) * 16777216.0);
            switch (tw_mem)
            {
                case 0:
                    FTW = FTW >> 12;
                    break;
                case 1:
                    FTW = FTW >> 11;
                    break;
                case 2:
                    FTW = FTW >> 10;
                    break;
                case 3:
                    FTW = FTW >> 9;
                    break;
                case 4:
                    FTW = FTW >> 8;
                    break;
                case 5:
                    FTW = FTW >> 7;
                    break;
                case 6:
                    FTW = FTW >> 6;
                    break;
                case 7:
                    FTW = FTW >> 5;
                    break;
                case 8:
                    FTW = FTW >> 4;
                    break;
                case 9:
                    FTW = FTW >> 3;
                    break;
                case 10:
                    FTW = FTW >> 2;
                    break;
                case 11:
                    FTW = FTW >> 1;
                    break;
                case 12:
                    break;
                case 13:
                    FTW = FTW & 0x07FF;
                    FTW = FTW << 1;
                    break; 
                case 14:
                    FTW = FTW & 0x03FF;
                    FTW = FTW << 2;
                    break;
                case 15:
                    FTW = FTW & 0x01FF;
                    FTW = FTW << 3;
                    break;
                case 16:
                    FTW = FTW & 0x00FF;
                    FTW = FTW << 4;
                    break;
            }
            //FTW = FTW & 0x00000FFF;
            return FTW;
        }

        /// <summary> </summary>
        /// <returns> </returns>
        public object   Clone               ( ) 
        {
            var objClone = new AD9106();
            return objClone;
        }

        /// <summary> Установка расчерка частоты в режиме линейной перестройки. </summary>
        /// <param name="bandwidth"> полоса перестройки. </param>
        public void     setRampBandwidth    ( double _bandwidth )
        {
            double fPoint    = 0.0F;
            double fRampMax  = 0.0;
            double fDdsMax   = 16777216.0F;

            RampBandwidth    = _bandwidth;

            fRampMax = (double)(RampBandwidth / (2.0 * this.RefClock));
            fRampMax = (double)(fRampMax * fDdsMax);

            for ( ushort i=0; i<12; i++ )
            {
                fPoint = fRampMax / (double)(1 << (12 - i));
                if (fPoint>1500)
                {
                    this.TwMem = (ushort)(i);
                    break;
                }
            }

            Points = (ulong)Math.Ceiling(fPoint);
        }

        /// <summary> Считывание полосы перестройки </summary>
        /// <returns> </returns>
        public double    getRampBandwidth    ( )
        {
            RampBandwidth  = (double)(1 << (12-this.TwMem));
            RampBandwidth *= ((double)Points);
            RampBandwidth = RampBandwidth / 16777216.0F;
            RampBandwidth = RampBandwidth * 170000000.0F;
            RampBandwidth = RampBandwidth * 2.0;
            return RampBandwidth;
        }

        /// <summary> TW_RAM_CONFIG </summary>
        public ushort    TwMem 
        {
            get
            {
                return this.registers[0x47];
            }
            set
            {
                this.registers[0x47] = value;
            }
        }

        /// <summary> Конструктор класса. </summary>
        public AD9106                       ( ) 
        {
        #region "INIT REGISTERS"
            if ( registers==null )
            {
                DefaultRegisters ( );
            }
        #endregion

            if ( Channels==null )
            {
                Channels    = new bool [4];
                Channels[0] = false;
                Channels[1] = false;
                Channels[2] = false;
                Channels[3] = true;
            }

            setPTW ( 0, 0x0000 );
            setPTW ( 1, 0x2000 );
            setPTW ( 2, 0x4000 );
            setPTW ( 3, 0x8000 );

            for ( int i=0; i<4; i++ )
            {
                if ( Channels[i]==true )
                {
                    setDigitalGain(i, DEFAULT_DIGITAL_GAIN);
                }
            }

            Start    = 1e6F;
            Step     = 5e5F;
            Points   = 10UL;
            Interval = 0.00001;

            RefClock = 170000000.0;
            FreqCtrl = 100000;

            MaxRamp  = 0x0FFF;
        }
    }

}
