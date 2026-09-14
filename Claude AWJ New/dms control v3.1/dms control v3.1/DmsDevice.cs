/**
 * @file:    DmsDevice.cs
 * @author:  Kirillov A.V.
 * @date:    22.12.2019
 * @email:   alexandrkirillov85@gmail.com
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

using nsAlexKir;
using nsAlexKir.Synthesizers;
using nsAlexKir.RF_Library;

namespace dms_control_v3 
{
    /// <summary> Настройка гетеродина. </summary>
    [Serializable]
    public class LoConfiguration 
    {
        /// <summary> Ожидание захвата СЧ. </summary>
        [XmlElement("WaitLD")]
        public int    WaitLD;
        
        /// <summary> Управление росчерком СЧ. </summary>
        [XmlElement("SweepOn")]
        public int    SweepOn;
        
        /// <summary> Шаг изменения частоты гетеродина. </summary>
        [XmlElement("Step")]
        public float  Step;
        
        /// <summary> Количество точек росчерка гетеродина. </summary>
        [XmlElement("Points")]
        public UInt32 Points;
        
        /// <summary> Время удержания частоты. </summary>
        [XmlElement("HoldTime")]
        public float  HoldTime;

        /// <summary> Синтезатор ФАПЧ. </summary>
        [XmlElement("PLL")]
        public MAX2871 pll;

        /// <summary> Конструктор класса. </summary>
        public LoConfiguration ( ) 
        {
            WaitLD   = 0;
            SweepOn  = 0;
            Step     = 1e6F;
            Points   = 10;
            HoldTime = 0.00002F;

            pll = new MAX2871 ( 50000000.0 );
        }
    }

    /// <summary> Управление детектором. </summary>
    [Serializable]
    public class DetectorCfg 
    {
        /// <summary> Управление детектором. </summary>
        [XmlElement("Control")]
        public ushort Control;

        /// <summary> Требуемый уровень напряжения в вольтах. </summary>
        [XmlElement("LimitVoltage")]
        public double LimitVoltage;

        /// <summary> Конструктор класса. </summary>
        /// <param name="_Control"></param>
        /// <param name="_LimitVoltage"></param>
        public DetectorCfg ( )
        {
            Control = 0;
            LimitVoltage = 0.0;
        }

        /// <summary> Конструктор класса. </summary>
        /// <param name="_Control"></param>
        /// <param name="_LimitVoltage"></param>
        public DetectorCfg ( ushort _Control, double _LimitVoltage )
        {
            Control = _Control;
            LimitVoltage = _LimitVoltage;
        }
    }

    /// <summary> Управление аттенюатором. </summary>
    [Serializable]
    public class Attenuator 
    {
        /// <summary> ЦАП для управления аттенюатором. </summary>
        [XmlElement("DacAtt")]
        public ushort Dac;

        /// <summary> Конструктор класса. </summary>
        public Attenuator ( )
        {
            Dac = 0;
        }

        /// <summary> Конструктор класса </summary>
        /// <param name="DacCode"> Код ЦАПа </param>
        public Attenuator ( ushort _Dac )
        {
            Dac = _Dac;
        }

        /// <summary> Управляющее напряжение. </summary>
        public double CtrlVoltage 
        {
            get
            {
                return ((double)Dac / 4096.0F) * 3.3F;
            }
            set
            {
                Dac = (ushort)((value / 3.3F) * 4096.0F);
            }
        }


    }

    /// <summary> </summary>
    public class AttenuatorValue
    {
        /// <summary> Ослабление </summary>
        public float  Attenuator;
        /// <summary> Напряжение управления </summary>
        public float  CtrlVoltage;
        
        /// <summary> Конструктор класса </summary>
        public AttenuatorValue ( float _Attenuator, float _CtrlVoltage )
        {
            this.Attenuator  = _Attenuator;
            this.CtrlVoltage = _CtrlVoltage;
        }
    }

    /// <summary> </summary>
    public class AttenuatorCalibrationTable
    {
        /// <summary> </summary>
        public List<AttenuatorValue> calibration_table;

        /// <summary> </summary>
        public AttenuatorCalibrationTable ()
        {
            calibration_table = new List<AttenuatorValue> ();

            calibration_table.Add ( new AttenuatorValue ( 0.0F,  0.0F  ) );
            calibration_table.Add ( new AttenuatorValue ( 1.0F,  0.1F  ) );
            calibration_table.Add ( new AttenuatorValue ( 2.0F,  0.2F  ) );
            calibration_table.Add ( new AttenuatorValue ( 3.0F,  0.3F  ) );
            calibration_table.Add ( new AttenuatorValue ( 4.0F,  0.4F  ) );
            calibration_table.Add ( new AttenuatorValue ( 5.0F,  0.5F  ) );
            calibration_table.Add ( new AttenuatorValue ( 6.0F,  0.6F  ) );
            calibration_table.Add ( new AttenuatorValue ( 7.0F,  0.65F ) );
            calibration_table.Add ( new AttenuatorValue ( 8.0F,  0.7F  ) );
            calibration_table.Add ( new AttenuatorValue ( 9.0F,  0.75F ) );
            calibration_table.Add ( new AttenuatorValue ( 10.0F, 0.8F  ) );
            calibration_table.Add ( new AttenuatorValue ( 11.0F, 0.9F  ) );
            calibration_table.Add ( new AttenuatorValue ( 12.0F, 1.0F  ) );
            calibration_table.Add ( new AttenuatorValue ( 13.0F, 1.1F  ) );
            calibration_table.Add ( new AttenuatorValue ( 14.0F, 1.2F  ) );
            calibration_table.Add ( new AttenuatorValue ( 15.0F, 1.25F ) );
            calibration_table.Add ( new AttenuatorValue ( 16.0F, 1.3F  ) );
            calibration_table.Add ( new AttenuatorValue ( 17.0F, 1.35F ) );
            calibration_table.Add ( new AttenuatorValue ( 18.0F, 1.4F  ) );
            calibration_table.Add ( new AttenuatorValue ( 19.0F, 1.45F ) );
            calibration_table.Add ( new AttenuatorValue ( 20.0F, 1.5F  ) );
            calibration_table.Add ( new AttenuatorValue ( 21.0F, 1.55F ) );
            calibration_table.Add ( new AttenuatorValue ( 22.0F, 1.6F  ) );
            calibration_table.Add ( new AttenuatorValue ( 23.0F, 1.65F ) );
            calibration_table.Add ( new AttenuatorValue ( 24.0F, 1.7F  ) );
            calibration_table.Add ( new AttenuatorValue ( 25.0F, 1.8F  ) );
            calibration_table.Add ( new AttenuatorValue ( 26.0F, 1.9F  ) );
            calibration_table.Add ( new AttenuatorValue ( 27.0F, 2.0F  ) );
        }

        /// <summary></summary>
        /// <param name="attenuation"></param>
        /// <returns></returns>
        public float getVoltage ( float attenuation ) 
        {
            float voltage = 0.0F;

            if (attenuation > 27.0F)    attenuation = 27.0F;
            if (attenuation < 0.0F)     attenuation = 0.0F;

            for ( int i=0; i<28; i++ )
            {
                if ( attenuation==calibration_table[i].Attenuator )
                {
                    voltage = calibration_table[i].CtrlVoltage;
                    break;
                }

                if ( attenuation>calibration_table[i].Attenuator && attenuation<calibration_table[i+1].Attenuator )
                {
                    voltage = (calibration_table[i+1].Attenuator-attenuation)/(calibration_table[i+1].Attenuator-calibration_table[i].Attenuator);
                    voltage = voltage * (calibration_table[i + 1].CtrlVoltage - calibration_table[i].CtrlVoltage);
                    voltage += calibration_table[i].CtrlVoltage;
                    break;
                }
            }

            return voltage;
        }

        /// <summary></summary>
        /// <param name="voltage"></param>
        /// <returns></returns>
        public float getAttenuation ( float voltage ) 
        {
            float attenuation = 0.0F;

            if ( voltage > 2.0F) voltage = 2.0F;
            if ( voltage < 0.0F) voltage = 0.0F;

            for ( int i=0; i<28; i++ )
            {
                if ( voltage==calibration_table[i].CtrlVoltage )
                {
                    attenuation = calibration_table[i].Attenuator;
                    break;
                }

                if ( voltage>calibration_table[i].CtrlVoltage && voltage<calibration_table[i+1].CtrlVoltage )
                {
                    attenuation  = (calibration_table[i+1].CtrlVoltage-voltage)/(calibration_table[i+1].CtrlVoltage-calibration_table[i].CtrlVoltage);
                    attenuation  = attenuation * (calibration_table[i + 1].Attenuator - calibration_table[i].Attenuator);
                    attenuation += calibration_table[i].Attenuator;
                    break;
                }
            }
            return attenuation;
        }
    }

    /// <summary> Класс управления динамика. </summary>
    [Serializable]    
    public class Buzzer 
    {
        /// <summary> Время задержки включения. </summary>
        [XmlElement("delay_on")]
        public float delay_on;

        /// <summary> Время включения. </summary>
        [XmlElement("time_on")]
        public float time_on;

        /// <summary> Конструктор класса. </summary>
        public Buzzer ( )
        {
            time_on     = 0.0F;
            delay_on    = 0.0F;
        }

        /// <summary> Конструктор класса. </summary>
        public Buzzer ( float _time_on=0, float _delay_on=0 ) 
        {
            time_on   = _time_on;
            delay_on  = _delay_on;
        }
    }

    /// <summary> Импульсный модулятор. </summary>
    [Serializable]
    public class PulseModulator
    {
        /// <summary> Сигнал управления. </summary>
        [XmlElement("ctrl")]
        public UInt16 ctrl;

        /// <summary> Частота управления встроенным переключателем </summary>
        [XmlElement("frequency")]
        public float frequency;

        /// <summary> Скважность импульса. </summary>
        [XmlElement("dutycycle")]
        public float dutycycle;

        /// <summary> Конструктор класса. </summary>
        public PulseModulator (  )
        {
            ctrl        = 0;
            frequency   = 0.0F;
            dutycycle   = 0.5F;
        }

        /// <summary> Конструктор класса. </summary>
        public PulseModulator ( UInt16 _ctrl=0, float _frequency=0.0F, float _dutycycle=0.5F )
        {
            ctrl        = _ctrl;
            frequency   = _frequency;
            dutycycle   = _dutycycle;
        }
    }

    [XmlRoot("DmsDevice", IsNullable = false)]
    public class DmsDevice 
    {
    #region USB Connection
        /// <summary> USB VID, для подключения к устройству.</summary>
        [XmlElement("vid")]
        public int vid;
        /// <summary> USB PID, для подключения к устройству.</summary>
        [XmlElement("pid")]
        public int pid;
    #endregion  /// USB Connection

        /// <summary> Список частот задаваемых переключателем. </summary>
        [XmlArray("DipStateFrequency")]
        public List<float> ltSwitchFrequency;

        /// <summary> Список частот задаваемых направление аттенюатора. </summary>
        [XmlArray("DipStateAttenuator")]
        public List<Attenuator> ltSwitchAttenuator;

        /// <summary> Калибровочная таблица. </summary>
        [XmlIgnore]        
        public AttenuatorCalibrationTable AttCalibrationTable;

        /// <summary> Настройка гетеродина. </summary>
        [XmlElement("lo")]
        public LoConfiguration lo;

        /// <summary> Настройка параметров DDS. </summary>
        [XmlElement("dds")]
        public AD9106 dds;

        /// <summary> Детектор мощности. </summary>
        [XmlElement("Detector")]
        public DetectorCfg detector;

        /// <summary> Настройка динамика. </summary>
        [XmlElement("Buzzer")]
        public Buzzer buzzer;

        /// <summary> Настройка импульсного модулятора. </summary>
        [XmlElement("pulse")]
        public PulseModulator pulse;

        /// <summary> Конструктор класса. </summary>
        public DmsDevice ( ) 
        {
            vid         = 1155;
            pid         = 22336;

            dds         = new AD9106 ();
            lo          = new LoConfiguration ();
            detector    = new DetectorCfg ();
            buzzer      = new Buzzer ();
            pulse       = new PulseModulator ();

            ltSwitchFrequency  = new List<float> ();
            ltSwitchAttenuator = new List<Attenuator> ();

            AttCalibrationTable = new AttenuatorCalibrationTable();

            CreateFrequencyList  ();
            CreateAttenuatorList ();
        }

        /// <summary> Создание списка частот. </summary>
        public void CreateFrequencyList     ( ) 
        {
            ltSwitchFrequency.Clear ( );
            for ( int i=0; i<8; i++ )
            {
                ltSwitchFrequency.Add ( (float)(2.5e9 + i*1e7) );
            }
        }

        /// <summary> Создание списка частот. </summary>
        public void CreateAttenuatorList    ( ) 
        {
            ltSwitchAttenuator.Clear ( );
            for ( int i=0; i<4; i++ )
            {
                ltSwitchAttenuator.Add ( new Attenuator(2048) );
            }
        }
    }
}