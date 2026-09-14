using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace nsAlexKir
{
    namespace Synthesizers
    {
        /// <summary> Регистр синтезатора частот MAX2871. </summary>
        [Serializable]
        [XmlRoot("RegisterMax2871")]
        public class RegisterMAX2871 : BaseRegister<int> 
        {
            /// <summary> Тело регистра. </summary>
            [XmlElement("body")]
            public override int body
            {
                get
                {
                    return (int)(data[3] << 24) + (int)(data[2] << 16) + (int)(data[1] << 8) + (int)data[0];
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
                    return (int)(data[0] & 0x07);
                }
                set
                {
                    data[0] = (byte)((data[0] & 0xF8) + (value & 0x07));
                }
            }

            /// <summary> Конструктор класса  </summary>
            public RegisterMAX2871 ( )
                : base(4)
            {
                body = 0;
            }
            /// <summary> Конструктор класса. </summary>
            public RegisterMAX2871 ( int _body )
                : base(4)
            {
                body = _body;
            }
        }

        /// <summary> Класс для работы с микросхемой MAX2871. </summary>
        [Serializable]
        [XmlRoot("Max2871")]
        public class MAX2871 : Synthesizer<RegisterMAX2871>, ICloneable
        {
            /// <summary> Минимальная частота микросхемы СЧ. </summary>
            [XmlElement("Min_Frequency")]
            public const float MinFrequency = 23500000.0F;
            /// <summary> Максимальная частота микросхемы СЧ. </summary>
            [XmlElement("Max_Frequency")]
            public const float MaxFrequency = 6000000000.0F;            
            /// <summary> массив настроек MUXOUT. </summary>
            [XmlIgnore]
            public String [] sMuxout = new String []
            {
                "Three-state output",
                "D_VDD",
                "D_GND",
                "R-divider output",
                "N-divider output/2",
                "Analog lock detect",
                "Digital lock detect",
                "Sync Input",
                "Reserved",
                "Reserved",
                "Reserved",
                "Read SPI registers 06",
            };

            [XmlIgnore]
            public String [] sPower = new String[]
            {
                "-4dBm", "-1dBm", "+2dBm", "+5dBm"
            };

            /// <summary> MUX Configuration </summary>
            /// <remarks> Sets MUX pin confguration (MSB bit located register 05).
            ///     0000 = Three-state output
            ///     0001 = D_VDD
            ///     0010 = D_GND
            ///     0011 = R-divider output
            ///     0100 = N-divider output/2
            ///     0101 = Analog lock detect
            ///     0110 = Digital lock detect
            ///     0111 = Sync Input
            ///     1000 : 1011 = Reserved
            ///     1100 = Read SPI registers 06
            ///     1101 : 1111= Reserved
            /// </remarks>
            [XmlIgnore]
            public override int MUXOUT 
            {
                get
                {
                    return (int)((registers[2].body >> 26) & 0x07) + (int)(((registers[5].body >> 18) & 0x01) << 3);
                }
                set
                {
                    int regL = (int)(registers[2].body & 0xE3FFFFFF);
                    int regH = (int)(registers[5].body & 0xFFFBFFFF);
                    registers[2].body = regL + ((value & 0x07) << 26);
                    registers[5].body = regH + (((value & 0x08) >> 3) << 18);
                }
            }

        #region REGISTER 0
            /// <summary> Int-N or Frac-N Mode Control. </summary>
            [XmlIgnore]
            public int IntFracMode
            {
                get
                {
                    return (registers[0].body >> 31) & 0x01;
                }
                set
                {
                    int reg = registers[0].body & 0x7FFFFFFF;
                    registers[0].body = reg + ((value & 0x01) << 31);
                }
            }
            /// <summary> Integer Division Value. </summary>
            [XmlIgnore]
            public override int N
            {
                get
                {
                    return (registers[0].body >> 15) & 0xFFFF;
                }
                set
                {
                    int reg = (int)(registers[0].body & 0x80007FFF);
                    registers[0].body = reg + (int)((value & 0xFFFF) << 15);
                }
            }
            /// <summary> Fractional Division Value. </summary>
            [XmlIgnore]
            public override int Frac
            {
                get
                {
                    return (int)((registers[0].body >> 3) & 0x0FFF);
                }
                set
                {
                    int reg = (int)(registers[0].body & 0xFFFF8007);
                    registers[0].body = reg + ((value & 0x0FFF) << 3);
                }
            }
        #endregion

        #region REGISTER 1
            /// <summary> Modulus Value ( M ). </summary>
            [XmlIgnore]
            public override int Mod
            {
                get
                {
                    return (int)((registers[1].body >> 3) & 0x0FFF);
                }
                set
                {
                    int reg = (int)(registers[1].body & 0xFFFF8007);
                    registers[1].body = reg + ((value & 0x0FFF) << 3);
                }
            }
            /// <summary> Phase Value ( M ). </summary>
            [XmlIgnore]
            public int Phase
            {
                get
                {
                    return (int)((registers[1].body >> 15) & 0x0FFF);
                }
                set
                {
                    int reg = (int)(registers[1].body & 0xF8007FFF);
                    registers[1].body = reg + ((value & 0x0FFF) << 15);
                }
            }
            /// <summary> Charge Pump Test. </summary>
            [XmlIgnore]
            public int CPT
            {
                get
                {
                    return (int)((registers[1].body >> 27) & 0x03);
                }
                set
                {
                    int reg = (int)(registers[1].body & 0xE7FFFFFF);
                    registers[1].body = reg + ((value & 0x03) << 27);
                }
            }
            /// <summary> Charge Pump Linearity. </summary>
            /// <remarks>
            /// Sets CP linearity mode.
            ///     00 = Disables the CP linearity mode (integer-N mode).
            ///     01 = CP linearity 10% mode (frac-N mode).
            ///     10 = CP linearity 20% mode (frac-N mode).
            ///     11 = CP linearity 30% mode (frac-N mode).
            /// </remarks>
            [XmlIgnore]
            public int CPL
            {
                get
                {
                    return (int)((registers[1].body >> 29) & 0x03);
                }
                set
                {
                    int reg = (int)(registers[1].body & 0x9FFFFFFF);
                    registers[1].body = reg + ((value & 0x03) << 29);
                }
            }
        #endregion

        #region REGISTER 2
            /// <summary> Lock-Detect Speed </summary>
            [XmlIgnore]
            public int LDS
            {
                get
                {
                    return (int)((registers[2].body >> 31) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[2].body & 0x7FFFFFFF);
                    registers[2].body = reg + ((value & 0x03) << 31);
                }
            }
            /// <summary> Frac-N Sigma Delta Noise Mode </summary>
            [XmlIgnore]
            public int SDN
            {
                get
                {
                    return (int)((registers[2].body >> 29) & 0x03);
                }
                set
                {
                    int reg = (int)(registers[2].body & 0x9FFFFFFF);
                    registers[2].body = reg + ((value & 0x03) << 29);
                }
            }
            /// <summary> Reference Double Mode </summary>
            [XmlIgnore]
            public int DBR
            {
                get
                {
                    return (int)((registers[2].body >> 25) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[2].body & 0xFDFFFFFF);
                    registers[2].body = reg + ((value & 0x01) << 25);
                }
            }
            /// <summary> Reference Div2 Mode </summary>
            [XmlIgnore]
            public int RDIV2
            {
                get
                {
                    return (int)((registers[2].body >> 24) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[2].body & 0xFEFFFFFF);
                    registers[2].body = reg + ((value & 0x01) << 24);
                }
            }
            /// <summary> Reference Divider Mode </summary>
            /// <remarks>
            /// Sets reference divide value (R). Double buffered by register 0.
            ///     0000000000 = 0 (unused)
            ///     0000000001 = 1
            ///     -----
            ///     1111111111 = 1023
            /// </remarks>
            [XmlIgnore]
            public override int R
            {
                get
                {
                    return (int)((registers[2].body >> 14) & 0x03FF);
                }
                set
                {
                    int reg = (int)(registers[2].body & 0xFF003FFF);
                    registers[2].body = reg + ((value & 0x03FF) << 14);
                }
            }
            /// <summary> Double Buffer </summary>
            /// <remarks>
            /// Sets double buffer mode.
            ///     0 = Disabled.
            ///     1 = Enabled.
            /// </remarks>
            [XmlIgnore]
            public int REG4DB
            {
                get
                {
                    return (int)((registers[2].body >> 13) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[2].body & 0xFFFFDFFF);
                    registers[2].body = reg + ((value & 0x01) << 13);
                }
            }
            /// <summary> Charge-Pump Current </summary>
            /// <remarks> 
            ///     Sets charge-pump current in mA (RSET = 5.1kΩ). Double buffered by register 0.
            ///     ICP = 1.63/RSET × (1+CP[3:0])
            /// </remarks>
            [XmlIgnore]
            public override int CP
            {
                get
                {
                    return (int)((registers[2].body >> 9) & 0x0F);
                }
                set
                {
                    int reg = (int)(registers[2].body & 0xFFFFE1FF);
                    registers[2].body = reg + ((value & 0x0F) << 9);
                }
            }
            /// <summary> Lock-Detect Function </summary>
            /// <remarks> Sets lock-detect function.
            ///     0 = Frac-N lock detect.
            ///     1 = Int-N lock detect.
            /// </remarks>
            [XmlIgnore]
            public int LDF
            {
                get
                {
                    return (int)((registers[2].body >> 8) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[2].body & 0xFFFFFEFF);
                    registers[2].body = reg + ((value & 0x01) << 8);
                }
            }
            /// <summary> Lock-Detect Precision </summary>
            [XmlIgnore]
            public int LDP
            {
                get
                {
                    return (int)((registers[2].body >> 7) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[2].body & 0xFFFFFF7F);
                    registers[2].body = reg + ((value & 0x01) << 7);
                }
            }
            /// <summary> Phase Detector Polarity </summary>
            [XmlIgnore]
            public int PDP
            {
                get
                {
                    return (int)((registers[2].body >> 6) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[2].body & 0xFFFFFFBF);
                    registers[2].body = reg + ((value & 0x01) << 6);
                }
            }
            /// <summary> Shutdown Mode </summary>
            [XmlIgnore]
            public int SHDN
            {
                get
                {
                    return (int)((registers[2].body >> 5) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[2].body & 0xFFFFFFDF);
                    registers[2].body = reg + ((value & 0x01) << 5);
                }
            }
            /// <summary> Charge Pump Output High Impedance Mode. </summary>
            [XmlIgnore]
            public int TRI
            {
                get
                {
                    return (int)((registers[2].body >> 4) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[2].body & 0xFFFFFFEF);
                    registers[2].body = reg + ((value & 0x01) << 4);
                }
            }
            /// <summary> Counter Reset </summary>
            /// <remarks>Sets counter reset mode:
            /// 0 = Normal operation.
            /// 1 = R and N counters reset.
            /// </remarks>
            [XmlIgnore]
            public int RST
            {
                get
                {
                    return (int)((registers[2].body >> 3) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[2].body & 0xFFFFFFF7);
                    registers[2].body = reg + ((value & 0x01) << 3);
                }
            }
        #endregion

        #region REGISTER 3
            /// <summary> Clock Divider Value. </summary>
            [XmlIgnore]
            public int CDIV
            {
                get
                {
                    return (int)((registers[3].body >> 3) & 0x0FFF);
                }
                set
                {
                    int reg = (int)(registers[3].body & 0xFFFF8007);
                    registers[3].body = reg + ((value & 0x0FFF) << 3);
                }
            }
            /// <summary> Clock Divider Mode. </summary>
            /// <remarks>
            /// Sets clock divider mode.
            ///     00 = Mute until Lock Delay.
            ///     01 = Fast-lock enabled.
            ///     10 = Phase Adjustment mode.
            ///     11 = Reserved.
            /// </remarks>
            [XmlIgnore]
            public int CDM
            {
                get
                {
                    return (int)((registers[3].body >> 15) & 0x03);
                }
                set
                {
                    int reg = (int)(registers[3].body & 0xFFFE7FFF);
                    registers[3].body = reg + ((value & 0x03) << 15);
                }
            }
            /// <summary> Mute Delay Mode. </summary>
            [XmlIgnore]
            public int MUTEDEL
            {
                get
                {
                    return (int)((registers[3].body >> 17) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[3].body & 0xFFFDFFFF);
                    registers[3].body = reg + ((value & 0x01) << 17);
                }
            }
            /// <summary> Cycle Slip Mode. </summary>
            [XmlIgnore]
            public int CSM
            {
                get
                {
                    return (int)((registers[3].body >> 18) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[3].body & 0xFFFBFFFF);
                    registers[3].body = reg + ((value & 0x01) << 18);
                }
            }
            /// <summary> VAS_TEMP. </summary>
            /// <remarks>
            /// Sets VAS response to temperature drift.
            ///     0 = VAS temperature compensation disabled.
            ///     1 = VAS temperature compensation enabled.
            /// </remarks>
            [XmlIgnore]
            public int VAS_TEMP
            {
                get
                {
                    return (int)((registers[3].body >> 24) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[3].body & 0xFEFFFFFF);
                    registers[3].body = reg + ((value & 0x01) << 24);
                }
            }
            /// <summary> VAS_SHDN. </summary>
            /// <remarks>
            /// Sets VAS shutdown mode.
            ///     0 = VAS enabled.
            ///     1 = VAS disabled.
            /// </remarks>
            [XmlIgnore]
            public int VAS_SHDN
            {
                get
                {
                    return (int)((registers[3].body >> 25) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[3].body & 0xFDFFFFFF);
                    registers[3].body = reg + ((value & 0x01) << 25);
                }
            }
            /// <summary> VCO. </summary>
            [XmlIgnore]
            public int VCO
            {
                get
                {
                    return (int)((registers[3].body >> 26) & 0x1F);
                }
                set
                {
                    int reg = (int)(registers[3].body & 0x07FFFFFF);
                    registers[3].body = reg + ((value & 0x1F) << 26);
                }
            }
        #endregion

        #region REGISTER 4
            /// <summary> RFOUTA Output Power. </summary>
            [XmlIgnore]
            public int APWR
            {
                get
                {
                    return (int)((registers[4].body >> 3) & 0x03);
                }
                set
                {
                    int reg = (int)(registers[4].body & 0xFFFFFFE7);
                    registers[4].body = reg + ((value & 0x03) << 3);
                }
            }
            /// <summary> RFOUTA Output Mode. </summary>
            [XmlIgnore]
            public int RFA_EN
            {
                get
                {
                    return (int)((registers[4].body >> 5) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[4].body & 0xFFFFFFDF);
                    registers[4].body = reg + ((value & 0x01) << 5);
                }
            }
            /// <summary> RFOUTB Output Power. </summary>
            [XmlIgnore]
            public int BPWR
            {
                get
                {
                    return (int)((registers[4].body >> 6) & 0x03);
                }
                set
                {
                    int reg = (int)(registers[4].body & 0xFFFFFF3F);
                    registers[4].body = reg + ((value & 0x03) << 6);
                }
            }
            /// <summary> RFOUTB Output Mode. </summary>
            [XmlIgnore]
            public int RFB_EN
            {
                get
                {
                    return (int)((registers[4].body >> 8) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[4].body & 0xFFFFFEFF);
                    registers[4].body = reg + ((value & 0x01) << 8);
                }
            }
            /// <summary> RFOUTB Output Path Select </summary>
            /// <remarks>
            /// Sets RFOUTB output path select.
            ///     0 = VCO divided output
            ///     1 = VCO fundamental frequency
            /// </remarks>
            [XmlIgnore]
            public int BDIV
            {
                get
                {
                    return (int)((registers[4].body >> 9) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[4].body & 0xFFFFFDFF);
                    registers[4].body = reg + ((value & 0x01) << 9);
                }
            }
            /// <summary> RFOUT Mute until Lock Detect </summary>
            /// <remarks>
            /// Sets RFOUT Mute until Lock Detect Mode
            ///     [0] = Disables RFOUT Mute until Lock Detect Mode.
            ///     [1] = Enables RFOUT Mute until Lock Detect Mode.
            /// </remarks>
            [XmlIgnore]
            public int MTLD
            {
                get
                {
                    return (int)((registers[4].body >> 10) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[4].body & 0xFFFFFBFF);
                    registers[4].body = reg + ((value & 0x01) << 10);
                }
            }
            /// <summary> RFOUT Mute until Lock Detect </summary>
            /// <remarks> 
            /// Sets VCO Shutdown mode.
            ///     0 = Enables VCO.
            ///     1 = Disables VCO.
            /// </remarks>
            [XmlIgnore]
            public int SDVCO
            {
                get
                {
                    return (int)((registers[4].body >> 11) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[4].body & 0xFFFFF7FF);
                    registers[4].body = reg + ((value & 0x01) << 11);
                }
            }
            /// <summary> Band Select </summary>
            [XmlIgnore]
            public int BS
            {
                get
                {
                    return (int)((registers[4].body >> 12) & 0xFF) + (int)(((registers[4].body >> 24) & 0x03) << 8);
                }
                set
                {
                    int reg = (int)(registers[4].body & 0xFCF00FFF);
                    registers[4].body = reg + ((value & 0xFF) << 12) + ((value >> 8) << 24);
                }
            }
            /// <summary> RFOUT_Output Divider Mode </summary>
            [XmlIgnore]
            public int DIVA
            {
                get
                {
                    return (int)((registers[4].body >> 20) & 0x07);
                }
                set
                {
                    int reg = (int)(registers[4].body & 0xFF8FFFFF);
                    registers[4].body = reg + ((value & 0x07) << 20);
                }
            }
            /// <summary> VCO Feedback Mode </summary>
            [XmlIgnore]
            public int FB
            {
                get
                {
                    return (int)((registers[4].body >> 23) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[4].body & 0xFF7FFFFF);
                    registers[4].body = reg + ((value & 0x01) << 23);
                }
            }
            /// <summary> Shutdown Reference Input </summary>
            [XmlIgnore]
            public int SDREF
            {
                get
                {
                    return (int)((registers[4].body >> 26) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[4].body & 0xFBFFFFFF);
                    registers[4].body = reg + ((value & 0x01) << 26);
                }
            }
            /// <summary> Shutdown VCO Divider </summary>
            [XmlIgnore]
            public int SDDIV
            {
                get
                {
                    return (int)((registers[4].body >> 27) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[4].body & 0xF7FFFFFF);
                    registers[4].body = reg + ((value & 0x01) << 27);
                }
            }
            /// <summary> Shutdown VCO LDO </summary>
            [XmlIgnore]
            public int SDLDO
            {
                get
                {
                    return (int)((registers[4].body >> 28) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[4].body & 0xEFFFFFFF);
                    registers[4].body = reg + ((value & 0x01) << 28);
                }
            }
        #endregion

        #region REGISTER 5
            /// <summary> ADC Mode </summary>
            [XmlIgnore]
            public int ADCM
            {
                get
                {
                    return (int)((registers[5].body >> 3) & 0x07);
                }
                set
                {
                    int reg = (int)(registers[5].body & 0xFFFFFFC7);
                    registers[5].body = reg + ((value & 0x07) << 3);
                }
            }
            /// <summary> ADC Start </summary>
            [XmlIgnore]
            public int ADCS
            {
                get
                {
                    return (int)((registers[5].body >> 6) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[5].body & 0xFFFFFFBF);
                    registers[5].body = reg + ((value & 0x01) << 6);
                }
            }
            /// <summary> Lock-Detect Pin Function </summary>
            [XmlIgnore]
            public int LD
            {
                get
                {
                    return (int)((registers[5].body >> 22) & 0x03);
                }
                set
                {
                    int reg = (int)(registers[5].body & 0xFF3FFFFF);
                    registers[5].body = reg + ((value & 0x03) << 22);
                }
            }
            /// <summary> Lock-Detect Pin Function </summary>
            [XmlIgnore]
            public int F01
            {
                get
                {
                    return (int)((registers[5].body >> 24) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[5].body & 0xFEFFFFFF);
                    registers[5].body = reg + ((value & 0x01) << 22);
                }
            }
            /// <summary> Shutdown PLL </summary>
            [XmlIgnore]
            public int SDPLL
            {
                get
                {
                    return (int)((registers[5].body >> 25) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[5].body & 0xFDFFFFFF);
                    registers[5].body = reg + ((value & 0x01) << 25);
                }
            }
            /// <summary> VAS_DLY </summary>
            [XmlIgnore]
            public int VAS_DLY
            {
                get
                {
                    return (int)((registers[5].body >> 29) & 0x03);
                }
                set
                {
                    int reg = (int)(registers[5].body & 0x9FFFFFFF);
                    registers[5].body = reg + ((value & 0x03) << 29);
                }
            }
        #endregion

        #region REGISTER 6
            /// <summary> Current VCO. </summary>
            [XmlIgnore]
            public int V
            {
                get
                {
                    return (int)((registers[6].body >> 3) & 0x3F);
                }
                set
                {
                    int reg = (int)(registers[6].body & 0xFFFFFF03);
                    registers[6].body = reg + ((value & 0x3F) << 3);
                }
            }
            /// <summary> VAS Active. </summary>
            [XmlIgnore]
            public int VASA
            {
                get
                {
                    return (int)((registers[6].body >> 9) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[6].body & 0xFFFFFDFF);
                    registers[6].body = reg + ((value & 0x01) << 9);
                }
            }
            /// <summary> ADC Valid </summary>
            [XmlIgnore]
            public int ADCV
            {
                get
                {
                    return (int)((registers[6].body >> 15) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[6].body & 0xFFFF7FFF);
                    registers[6].body = reg + ((value & 0x01) << 15);
                }
            }
            /// <summary> ADC Code </summary>
            [XmlIgnore]
            public int ADC
            {
                get
                {
                    return (int)((registers[6].body >> 16) & 0x7F);
                }
                set
                {
                    int reg = (int)(registers[6].body & 0xFF80FFFF);
                    registers[6].body = reg + ((value & 0x7F) << 16);
                }
            }
            /// <summary> Power On Reset </summary>
            [XmlIgnore]
            public int POR
            {
                get
                {
                    return (int)((registers[6].body >> 23) & 0x01);
                }
                set
                {
                    int reg = (int)(registers[6].body & 0xFF7FFFFF);
                    registers[6].body = reg + ((value & 0x01) << 23);
                }
            }
            /// <summary> Die ID </summary>
            [XmlIgnore]
            public int DIE
            {
                get
                {
                    return (int)((registers[6].body >> 28) & 0x0F);
                }
                set
                {
                    int reg = (int)(registers[6].body & 0x0FFFFFFF);
                    registers[6].body = reg + ((value & 0x0F) << 28);
                }
            }
        #endregion

            /// <summary>Функция определения делителя частоты ГУН</summary>
            /// <param name="freq">Частота на выходе прибора</param>
            /// <returns>Установленный делитель</returns>
            /// <remarks> Sets RFOUT_ output divider mode. Double buffered by register 0 when REG4DB = 1.
            /// 000 = Divide by 1, if 3000MHz ≤ fRFOUTA ≤ 6000MHz
            /// 001 = Divide by 2, if 1500MHz ≤ fRFOUTA < 3000MHz
            /// 010 = Divide by 4, if 750MHz ≤ fRFOUTA < 1500MHz
            /// 011 = Divide by 8, if 375MHz ≤ fRFOUTA < 750MHz
            /// 100 = Divide by 16, if 187.5MHz ≤ fRFOUTA < 375MHz
            /// 101 = Divide by 32, if 93.75MHz ≤ fRFOUTA < 187.5MHz
            /// 110 = Divide by 64, if 46.875MHz ≤ fRFOUTA < 93.75MHz
            /// 111 = Divide by 128, if 23.5MHz ≤ fRFOUTA < 46.875MHz
            /// </remarks>
            public override int defineVcoDiv ( double freq ) 
            {
                if (freq >= 3e9 && freq <= 6e9) return 1;
                else if (freq >= 1.5e9 && freq < 3e9) return 2;
                else if (freq >= 0.75e9 && freq < 1.5e9) return 4;
                else if (freq >= 375e6 && freq < 750e6) return 8;
                else if (freq >= 187.5e6 && freq < 375e6) return 16;
                else if (freq >= 93.75e6 && freq < 187.5e6) return 32;
                else if (freq >= 46.875e6 && freq < 93.75e6) return 64;
                else if (freq >= 23.5e6 && freq < 46.875e6) return 128;
                else return 1;
            }

            /// <summary> Множитель опорной частоты. </summary>
            [XmlIgnore]
            public override int Mul_R 
            {
                get
                {
                    int mul = (int)(Math.Pow(2, DBR));
                    return mul;
                }
            }

            /// <summary> Делитель частоты ГУНа. </summary>
            [XmlIgnore]
            public override double VcoDiv 
            {
                get
                {
                    return Math.Pow(2, DIVA);
                }
                set
                {
                    DIVA = (int)Math.Log(value, 2);
                }
            }

            /// <summary> Выходная частота. </summary>
            [XmlIgnore]
            public override double OutFrequency 
            {
                get
                {  
                    double outfreq  = 0.0;
                    double pfd      = 0.0;
                    double mod      = (this.Mod == 0) ? 1 : this.Mod;

                    pfd = (1 + DBR) / (this.R * (1 + this.RDIV2));
                    pfd *= this.RefFrequency;
                    outfreq = pfd * (this.N + this.Frac / mod);

                    return outfreq / VcoDiv;
                    //return base.OutFrequency;
                }
                set
                {
                    base.OutFrequency = value;
                    BS = (int)(RefFrequency / 50000.0);
                }
            }

            /// <summary></summary>
            /// <returns></returns>
            public object Clone()
            {
                var objClone = new MAX2871();
                 
                return objClone;
            }

            /// <summary> Конструктор класса. </summary>
            /// <param name="_RefFrequency">Опорная частота микросхемы MAX2871.</param>
            public MAX2871()
                : base ( 24e6, "MAX2871" ) 
            {
                registers    = new RegisterMAX2871[7];
                registers[0] = new RegisterMAX2871(0x00000000);
                registers[1] = new RegisterMAX2871(0x00000001);
                registers[2] = new RegisterMAX2871(0x00000002);
                registers[3] = new RegisterMAX2871(0x00000003);
                registers[4] = new RegisterMAX2871(0x60000004);
                registers[5] = new RegisterMAX2871(0x00000005);
                registers[6] = new RegisterMAX2871(0x00000006);
                initSequence = new int[] { 5, 4, 3, 2, 1, 0, 5, 4, 3, 2, 1, 0 };

            #region Init registers
                //  Регистр 0
                IntFracMode = 0;    //  0 = Enable the fractional-N mode
                //  Регистр 1
                CPL = 1;            //  CP Linearity 20% mode (frac-N mode)
                CPT = 0;            //  Normal mode
                P = 1;              //  Sets phase value. See the Phase Adjustment section.
                //  Регистр 2
                LDS = 0;            //  Lock-detect speed adjustment, Fpfd <= 32 MHz
                SDN = 0;            //  Low-noise mode
                MUXOUT = 2;         //  D_GND
                DBR = 0;            //  Reference Double Mode.
                RDIV2 = 0;          //  Reference Div2 Mode, 0 - Disable reference divide-by-2.
                R = 1;              //  R counter,
                REG4DB = 0;         //  Sets double buffer mode, 0 - Disable.
                CP = 15;            //  Charge-Pump Current. ICP = 1.63/RSET x (1 + CP[3:0])
                LDF = 0;            //  Lock-Detect Function, INT-N lock detect
                LDP = 0;            //  Lock-Detect Precision, 0 = 10 ns
                PDP = 1;            //  Phase Detector Polarity, 1 = Positive ( default )
                SHDN = 0;           //  Shutdown  Mode, 0 = Normal mode
                TRI = 0;            //  Charge Pump Output High-Impedance Mode, 
                RST = 0;            //  Counter Reset, Normal operation
                // Регистр 3
                VCO = 0;            //  Manual selection of VCO
                VAS_SHDN = 0;       //  VAS enabled
                VAS_TEMP = 0;       //  Sets VAS response to temperature drift. 0 = VAS temperature compensation disabled. 1 = VAS temperature compensation enabled.
                CSM = 0;            //  Cycle Slip Mode. [0] = Disable Cycle Slip Reduction. [1] = Enable Cycle Slip Reduction.
                MUTEDEL = 0;        //  Mute Delay. [0] = Do not delay LD to MTLD function to prevent flickering. [1] = Delay LD to MTLD function to prevent flickering.
                CDM = 0;            //  Sets clock divider mode.
                CDIV = 1;           //  Sets 12-bit clock divider mode.
                // Регистр 4
                SDLDO = 0;          //  Enables LDO
                SDDIV = 0;          //  Enables VCO Divider
                SDREF = 0;          //  Enables Referenve Input,
                SDVCO = 0;          //  Sets VCO Shutdown mode. 0 = Enables VCO. 1 = Disables VCO
                MTLD = 0;           //  RFOUT Mute until Lock Detect, 0 - Disable
                BDIV = 1;           //  RFOUTB output path select, VCO fundamental frequency
                FB = 1;             //  VCO Feedback Mode, 0 - Divided, 1 - Fundamental
                RFB_EN = 0;         //  Sets RFOUTB output mode, Disabled.
                BPWR = 0;           //  RFOUTB = -4 dBm
                RFA_EN = 1;         //  Sets RFOUTA output mode, Disabled.
                APWR = 1;           //  RFOUTA = -4 dBm            
                BS = 480;           //  Band Select
                // Регистр 5
                SDPLL = 0;          //  Enables PLL
                F01 = 0;            //  Sets integer mode for F = 0. [0] = If F[11:0] = 0, then fractional-N mode is set. [1] = If F[11:0] = 0, then integer-N mode is auto set.
                LD = 1;             //  Digital lock detect
            #endregion

                HighFrequency = 6e9;
                LowFrequency = 23.5e6;

                LowVcoFrequency = 3e9;
                HighVcoFrequency = 6e9;
            }

            /// <summary> Конструктор класса. </summary>
            /// <param name="_RefFrequency">Опорная частота микросхемы MAX2871.</param>
            public MAX2871 ( double _RefFrequency ) 
                : base ( _RefFrequency, "MAX2871" ) 
            {
                registers    = new RegisterMAX2871[7];
                registers[0] = new RegisterMAX2871(0x00000000);
                registers[1] = new RegisterMAX2871(0x00000001);
                registers[2] = new RegisterMAX2871(0x00000002);
                registers[3] = new RegisterMAX2871(0x00000003);
                registers[4] = new RegisterMAX2871(0x60000004);
                registers[5] = new RegisterMAX2871(0x00000005);
                registers[6] = new RegisterMAX2871(0x00000006);
                initSequence = new int[] { 5, 4, 3, 2, 1, 0, 5, 4, 3, 2, 1, 0 };

            #region Init registers
                //  Регистр 0
                IntFracMode = 0;    //  0 = Enable the fractional-N mode
                //  Регистр 1
                CPL = 1;            //  CP Linearity 20% mode (frac-N mode)
                CPT = 0;            //  Normal mode
                P = 1;              //  Sets phase value. See the Phase Adjustment section.
                //  Регистр 2
                LDS = 0;            //  Lock-detect speed adjustment, Fpfd <= 32 MHz
                SDN = 0;            //  Low-noise mode
                MUXOUT = 2;         //  D_GND
                DBR = 0;            //  Reference Double Mode.
                RDIV2 = 0;          //  Reference Div2 Mode, 0 - Disable reference divide-by-2.
                R = 1;              //  R counter,
                REG4DB = 0;         //  Sets double buffer mode, 0 - Disable.
                CP = 15;            //  Charge-Pump Current. ICP = 1.63/RSET x (1 + CP[3:0])
                LDF = 0;            //  Lock-Detect Function, INT-N lock detect
                LDP = 0;            //  Lock-Detect Precision, 0 = 10 ns
                PDP = 1;            //  Phase Detector Polarity, 1 = Positive ( default )
                SHDN = 0;           //  Shutdown  Mode, 0 = Normal mode
                TRI = 0;            //  Charge Pump Output High-Impedance Mode, 
                RST = 0;            //  Counter Reset, Normal operation
                // Регистр 3
                VCO = 0;            //  Manual selection of VCO
                VAS_SHDN = 0;       //  VAS enabled
                VAS_TEMP = 0;       //  Sets VAS response to temperature drift. 0 = VAS temperature compensation disabled. 1 = VAS temperature compensation enabled.
                CSM = 0;            //  Cycle Slip Mode. [0] = Disable Cycle Slip Reduction. [1] = Enable Cycle Slip Reduction.
                MUTEDEL = 0;        //  Mute Delay. [0] = Do not delay LD to MTLD function to prevent flickering. [1] = Delay LD to MTLD function to prevent flickering.
                CDM = 0;            //  Sets clock divider mode.
                CDIV = 1;           //  Sets 12-bit clock divider mode.
                // Регистр 4
                SDLDO = 0;          //  Enables LDO
                SDDIV = 0;          //  Enables VCO Divider
                SDREF = 0;          //  Enables Referenve Input,
                SDVCO = 0;          //  Sets VCO Shutdown mode. 0 = Enables VCO. 1 = Disables VCO
                MTLD = 0;           //  RFOUT Mute until Lock Detect, 0 - Disable
                BDIV = 1;           //  RFOUTB output path select, VCO fundamental frequency
                FB = 1;             //  VCO Feedback Mode, 0 - Divided, 1 - Fundamental
                RFB_EN = 0;         //  Sets RFOUTB output mode, Disabled.
                BPWR = 0;           //  RFOUTB = -4 dBm
                RFA_EN = 1;         //  Sets RFOUTA output mode, Disabled.
                APWR = 1;           //  RFOUTA = -4 dBm            
                BS = 480;           //  Band Select
                // Регистр 5
                SDPLL = 0;          //  Enables PLL
                F01 = 0;            //  Sets integer mode for F = 0. [0] = If F[11:0] = 0, then fractional-N mode is set. [1] = If F[11:0] = 0, then integer-N mode is auto set.
                LD = 1;             //  Digital lock detect
            #endregion

                HighFrequency = 6e9;
                LowFrequency = 23.5e6;

                LowVcoFrequency = 3e9;
                HighVcoFrequency = 6e9;
            }

            /// <summary> Конструктор класса. </summary>
            public MAX2871 ( double _RefFrequency, String _Name )
                : base( _RefFrequency, _Name ) 
            {
                registers    = new RegisterMAX2871 [ 7 ];
                registers[0] = new RegisterMAX2871 ( 0x00000000 );
                registers[1] = new RegisterMAX2871 ( 0x00000001 );
                registers[2] = new RegisterMAX2871 ( 0x00000002 );
                registers[3] = new RegisterMAX2871 ( 0x00000003 );
                registers[4] = new RegisterMAX2871 ( 0x60000004 );
                registers[5] = new RegisterMAX2871 ( 0x00000005 );
                registers[6] = new RegisterMAX2871 ( 0x00000006 );
                initSequence = new int[] { 5, 4, 3, 2, 1, 0, 5, 4, 3, 2, 1, 0 };

            #region INIT REGISTERS
                //  Регистр 0
                IntFracMode = 0;    //  0 = Enable the fractional-N mode
                //  Регистр 1
                CPL = 1;            //  CP Linearity 20% mode (frac-N mode)
                CPT = 0;            //  Normal mode
                P = 1;              //  Sets phase value. See the Phase Adjustment section.
                //  Регистр 2
                LDS = 0;            //  Lock-detect speed adjustment, Fpfd <= 32 MHz
                SDN = 0;            //  Low-noise mode
                MUXOUT = 2;         //  D_GND
                DBR = 0;            //  Reference Double Mode.
                RDIV2 = 0;          //  Reference Div2 Mode, 0 - Disable reference divide-by-2.
                R = 1;              //  R counter,
                REG4DB = 0;         //  Sets double buffer mode, 0 - Disable.
                CP = 15;            //  Charge-Pump Current. ICP = 1.63/RSET x (1 + CP[3:0])
                LDF = 0;            //  Lock-Detect Function, INT-N lock detect
                LDP = 0;            //  Lock-Detect Precision, 0 = 10 ns
                PDP = 1;            //  Phase Detector Polarity, 1 = Positive ( default )
                SHDN = 0;           //  Shutdown  Mode, 0 = Normal mode
                TRI = 0;            //  Charge Pump Output High-Impedance Mode, 
                RST = 0;            //  Counter Reset, Normal operation
                // Регистр 3
                VCO = 0;            //  Manual selection of VCO
                VAS_SHDN = 0;       //  VAS enabled
                VAS_TEMP = 0;       //  Sets VAS response to temperature drift. 0 = VAS temperature compensation disabled. 1 = VAS temperature compensation enabled.
                CSM = 0;            //  Cycle Slip Mode. [0] = Disable Cycle Slip Reduction. [1] = Enable Cycle Slip Reduction.
                MUTEDEL = 0;        //  Mute Delay. [0] = Do not delay LD to MTLD function to prevent flickering. [1] = Delay LD to MTLD function to prevent flickering.
                CDM = 0;            //  Sets clock divider mode.
                CDIV = 1;           //  Sets 12-bit clock divider mode.
                // Регистр 4
                SDLDO = 0;          //  Enables LDO
                SDDIV = 0;          //  Enables VCO Divider
                SDREF = 0;          //  Enables Referenve Input,
                SDVCO = 0;          //  Sets VCO Shutdown mode. 0 = Enables VCO. 1 = Disables VCO
                MTLD = 0;           //  RFOUT Mute until Lock Detect, 0 - Disable
                BDIV = 1;           //  RFOUTB output path select, VCO fundamental frequency
                FB = 1;             //  VCO Feedback Mode, 0 - Divided, 1 - Fundamental
                RFB_EN = 0;         //  Sets RFOUTB output mode, Disabled.
                BPWR = 0;           //  RFOUTB = -4 dBm
                RFA_EN = 1;         //  Sets RFOUTA output mode, Disabled.
                APWR = 1;           //  RFOUTA = -4 dBm            
                BS = 480;           //  Band Select
                // Регистр 5
                SDPLL = 0;          //  Enables PLL
                F01 = 0;            //  Sets integer mode for F = 0. [0] = If F[11:0] = 0, then fractional-N mode is set. [1] = If F[11:0] = 0, then integer-N mode is auto set.
                LD = 1;             //  Digital lock detect
            #endregion

                HighFrequency       = 6e9;
                LowFrequency        = 23.5e6;

                LowVcoFrequency     = 3e9;
                HighVcoFrequency    = 6e9;
            }
        }
    }
}
