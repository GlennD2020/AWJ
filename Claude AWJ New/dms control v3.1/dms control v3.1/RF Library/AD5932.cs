using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace nsAlexKir
{
    /// <summary> Объект микросхемы AD5932. </summary>
    [Serializable]
    [XmlRoot("ad5932")]
    public class AD5932
    {
        /// <summary> Массив регистров. </summary>
        [XmlArray("registers")]
        public AD5932_Register[] registers;

        /// <summary> Конструктор класса. </summary>
        public AD5932 () 
        {
            registers = new AD5932_Register [16];

            registers [0]  = new AD5932_Register ( 0x00D3 );
            registers [1]  = new AD5932_Register ( 0x1000 );
            registers [2]  = new AD5932_Register ( 0x2000 );
            registers [3]  = new AD5932_Register ( 0x3000 );
            registers [4]  = new AD5932_Register ( 0x4000 );
            registers [5]  = new AD5932_Register ( 0x5000 );
            registers [6]  = new AD5932_Register ( 0x6000 );
            registers [7]  = new AD5932_Register ( 0x7000 );
            registers [8]  = new AD5932_Register ( 0x8000 );
            registers [9]  = new AD5932_Register ( 0x9000 );
            registers [10] = new AD5932_Register ( 0xA000 );
            registers [11] = new AD5932_Register ( 0xB000 );
            registers [12] = new AD5932_Register ( 0xC000 );
            registers [13] = new AD5932_Register ( 0xD000 );
            registers [14] = new AD5932_Register ( 0xE000 );
            registers [15] = new AD5932_Register ( 0xF000 );

            DACEN = 1;
        }

        /// <summary> Установка регистра. </summary>
        /// <param name="register"> Значение регистра. </param>
        public void SetRegister ( ushort register ) 
        {
            int address = (int)((register & 0xF000) >> 12);
            registers[address].body = (ushort)register;
        }

    #region REGISTER 0
        /// <summary> Size of register 24-bit/12-bit.</summary>
        [XmlIgnore]
        public int B24 
        {
            get
            {
                int value = ( ( registers[0].body & 0x0800 ) > 0 ) ? 1 : 0;
                return value;
            }
            set
            {
                ushort reg = registers[0].body;
                reg = (ushort)( reg & 0xF7FF );
                reg = (ushort)( reg + ((value & 0x01) << 11) );
                registers[0].body = reg;
            }
        }

        /// <summary> DAC ENABLE </summary>
        [XmlIgnore]
        public int DACEN 
        {
            get
            {
                int value = ((registers[0].body & 0x0400) > 0) ? 1 : 0;
                return value;
            }
            set
            {
                ushort reg = registers[0].body;
                reg = (ushort)(reg & 0xFBFF);
                reg = (ushort)(reg + ((value & 0x01) << 10));
                registers[0].body = reg;
            }
        }

        /// <summary> SIN/TRI </summary>
        [XmlIgnore]
        public int SIN_TRI 
        {
            get
            {
                int value = ((registers[0].body & 0x0200) > 0) ? 1 : 0;
                return value;
            }
            set
            {
                ushort reg = registers[0].body;
                reg = (ushort)(reg & 0xFDFF);
                reg = (ushort)(reg + ((value & 0x01) << 9));
                registers[0].body = reg;
            }
        }

        /// <summary>
        /// When MSBOUTEN = 1, the MSBOUT pin is enabled.
        /// When MSBOUTEN = 0, the MSBOUT is disabled (three-state).
        /// </summary>
        [XmlIgnore]
        public int MSBOUTEN 
        {
            get
            {
                int value = ((registers[0].body & 0x0100) > 0) ? 1 : 0;
                return value;
            }
            set
            {
                ushort reg = registers[0].body;
                reg = (ushort)(reg & 0xFEFF);
                reg = (ushort)(reg + ((value & 0x01) << 8));
                registers[0].body = reg;
            }
        }

        /// <summary>
        /// When INT/EXT INCR = 1, the frequency increments are triggered externally through the CTRL pin.
        /// When INT/EXT INCR = 0, the frequency increments are triggered automatically.
        /// </summary>
        [XmlIgnore]
        public int INT_EXT 
        {
            get
            {
                int value = ((registers[0].body & 0x0020) > 0) ? 1 : 0;
                return value;
            }
            set
            {
                ushort reg = registers[0].body;
                reg = (ushort)(reg & 0xFFDF);
                reg = (ushort)(reg + ((value & 0x01) << 5));
                registers[0].body = reg;
            }
        }

        /// <summary>
        /// This bit is active when D2 = 1. It is user-selectable to pulse at end of scan (EOS) or at each frequency
        /// increment. When SYNCSEL = 1, the SYNCOUT pin outputs a high level at end of scan and returns to 0 at the start of the subsequent scan.
        /// </summary>
        [XmlIgnore]
        public int SYNCSEL 
        {
            get
            {
                int value = ((registers[0].body & 0x0008) > 0) ? 1 : 0;
                return value;
            }
            set
            {
                ushort reg = registers[0].body;
                reg = (ushort)(reg & 0xFFF7);
                reg = (ushort)(reg + ((value & 0x01) << 3));
                registers[0].body = reg;
            }
        }

        /// <summary>
        /// When SYNCOUTEN = 1, the SYNC output is available at the SYNCOUT pin.
        /// When SYNCOUTEN = 0, the SYNCOP pin is disabled (three-state).
        /// </summary>
        [XmlIgnore]
        public int SYNCOUTEN 
        {
            get
            {
                int value = ((registers[0].body & 0x0004) > 0) ? 1 : 0;
                return value;
            }
            set
            {
                ushort reg = registers[0].body;
                reg = (ushort)(reg & 0xFFFB);
                reg = (ushort)(reg + ((value & 0x01) << 2));
                registers[0].body = reg;
            }
        }
    #endregion  //  REGISTER 0

        /// <summary> Start frequency. </summary>
        [XmlIgnore]
        public double   StartFrequency 
        {
            get
            {
                ushort regL = (ushort)( registers[12].value );
                ushort regH = (ushort)( registers[13].value );
                ulong ulValue = ((ulong)(regH << 12)) + (ulong)regL;
                return (((double)ulValue) / (16777216.0)) * 50e6;
            }
            set
            {
                double dValue = ( value > 25e6 ) ? 25e6 : value;

                ulong ulValue = (ulong)(( dValue / 50e6 ) * ( 1 << 24 ));
                registers[12].value = (ushort)( (ulValue>>0) & 0x0FFF);
                registers[13].value = (ushort)( (ulValue>>12) & 0x0FFF);
            }
        }

        /// <summary> Number increments. </summary>
        /// <remarks> Numbers of points. </remarks>
        [XmlIgnore]
        public ushort   NumberIncrements 
        {
            get
            {
                return (ushort)(registers[1].value & 0x0FFF);
            }
            set
            {
                ushort usValue = value;
                if (usValue > 0x0FFF) usValue = 0x0FFF;

                /// Проверка вводимого значения точек росчерка.
                double dTempValue = StartFrequency + ((double)usValue) * DeltaFrequency;
                if (dTempValue>25e6) dTempValue = 25e6;

                dTempValue -= StartFrequency;
                dTempValue = dTempValue / DeltaFrequency;

                usValue = (ushort)Math.Floor(dTempValue);

                registers[1].value = usValue;
            }
        }

        /// <summary> Delta frequency. </summary>
        /// <remarks> Step of the frequency. </remarks>
        [XmlIgnore]
        public double   DeltaFrequency 
        {
            get
            {
                ulong ulValue = (ulong)(registers[2].value);
                ulValue = ulValue + ((ulong)(registers[3].value) << 12);                
                return (((double)ulValue) / (16777216.0)) * 50e6;
            }
            set
            {
                double dValue = (value > 25e6) ? 25e6 : value;
                double dTempValue = 0.0;

                dTempValue = StartFrequency + (double)NumberIncrements * dValue;
                if ( dTempValue>25e6 ) 
                {
                    dTempValue = 25e6;
                    dTempValue = dTempValue - StartFrequency;

                    dValue = dTempValue / (double)NumberIncrements;
                }

                ulong ulValue = (ulong)( ( dValue / 50e6) * (1<<24 ));
                registers[2].value = (ushort)(ulValue & 0x0FFF);
                registers[3].value = (ushort)((ulValue >> 12) & 0x0FFF);
            }
        }

        /// <summary> Increment interval. </summary>
        [XmlIgnore]
        public double   IncInterval 
        {
            get
            {
                return (double)(registers[6].value)/50e6;
            }
            set
            {
                if (value > 0.00004) value = 0.00004;
                registers[6].value = (ushort)(value * 50e6);
            }
        }

        /// <summary> Control Frequency. </summary>
        [XmlIgnore]
        public double   CtrlFrequency 
        {
            get
            {
                double ctrl_frequency = 0;
                if (this.NumberIncrements != 1)
                    ctrl_frequency = (1.0 / (this.NumberIncrements * this.IncInterval));
                return ctrl_frequency;
            }
        }

        /// <summary> Span of frequency. </summary>
        [XmlIgnore]
        public double   SpanFrequency 
        {
            get
            {
                return DeltaFrequency * ((double)NumberIncrements);
            }
        }
    }

    /// <summary> Объект регистра. </summary>
    [Serializable]
    [XmlRoot("register")]
    public class AD5932_Register : BaseRegister<ushort>
    {
        /// <summary> Адрес регистра. </summary>
        [XmlIgnore]
        public override int address 
        {
            get
            {
                return (data[1] >> 4) & 0xF;
            }
            set
            {
                data[1] = (byte)(data[1] & 0x0F);
                data[1] = (byte)(((value & 0x0F) << 4) + data[1]);
            }
        }
        /// <summary> Тело регистра. </summary>
        [XmlElement("body")]
        public override ushort body 
        {
            get
            {
                return (ushort)((data[1] << 8) + data[0]);
            }
            set
            {
                data[0] = (byte)(value & 0xFF);
                data[1] = (byte)((value >> 8) & 0xFF);
            }
        }
        /// <summary> Значение регистра. </summary>
        [XmlIgnore]
        public override ushort value 
        {
            get
            {
                return (ushort)(body & 0x0FFF);
            }
            set
            {
                ushort val = (ushort)(body & 0xF000);
                val = (ushort)(val + (value & 0x0FFF));
                body = val;
            }
        }
        /// <summary> Конструктор класса. </summary>
        /// <param name="value"> Значение регистра. </param>
        public AD5932_Register ( ) : base ( 2 )
        {
            body = 0;
        }
        /// <summary> Конструктор класса. </summary>
        /// <param name="value"> Значение регистра. </param>
        public AD5932_Register ( ushort _body ) : base ( 2 ) 
        {
            body = _body;
        }
    }
}
