/**
 * @file:       Synthesizer.cs
 * @author:     Kirillov A.V.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace nsAlexKir
{
    /// Пространство имен для Синтезатора Частот.
    namespace Synthesizers
    {
        /// <summary>Базовый класс синтезатора частот.</summary>
        public class Synthesizer<TypeRegister>
        {
            /// <summary> Минимальная частота СЧ. </summary>
            [XmlIgnore]
            public double LowFrequency = 1e8;

            /// <summary> Максимальная частота СЧ. </summary>
            [XmlIgnore]
            public double HighFrequency = 6e9;

            /// <summary> Минимальная частота ГУН. </summary>
            [XmlIgnore]
            public double LowVcoFrequency = 3e9;

            /// <summary> Максимальная частота ГУН. </summary>
            [XmlIgnore]
            public double HighVcoFrequency = 6e9;

            /// <summary> Массив регистров. </summary>
            [XmlArray("registers")]
            public TypeRegister[] registers;

            /// <summary> Инициализационная последовательность. </summary>
            [XmlArray("init_seq")]
            [XmlArrayItem("reg_num")]
            public int[] initSequence;
            
            /// <summary> Имя класса. </summary>
            [XmlElement("Name")]
            public String Name;

            /// <summary> Опорная частота. </summary>
            [XmlElement("RefFrequency")]
            public double RefFrequency;

            /// <summary> R Divider. </summary>
            [XmlIgnore]
            public virtual int R 
            {
                get { return 1; }
                set { }
            }
            /// <summary> A Divider </summary>
            [XmlIgnore]
            public virtual int A 
            {
                get { return 1; }
                set { }
            }
            /// <summary> B Divider </summary>
            [XmlIgnore]
            public virtual int B 
            {
                get { return 1; }
                set { }
            }
            /// <summary> Prescaler. </summary>
            [XmlIgnore]
            public virtual int P 
            {
                get { return 1; }
                set { }
            }
            /// <summary> Muxout control </summary>
            [XmlIgnore]
            public virtual int MUXOUT 
            {
                get { return 1; }
                set { }
            }
            /// <summary> Charge-Pump Current </summary>
            [XmlIgnore]
            public virtual int CP 
            {
                get { return 0; }
                set { }
            }
            /// <summary> Множитель опорной частоты. </summary>
            [XmlIgnore]
            public virtual int Mul_R 
            {
                get { return 1; }
                set { }
            }
            /// <summary> Частота Частотно-Фазового Детектора. </summary>
            [XmlIgnore]
            public virtual double PFD 
            {
                get
                {
                    return (double)(RefFrequency / (double)R) * ((double)Mul_R);
                }
            }
            /// <summary> Целочисленный делитель частоты ГУН. </summary>
            [XmlIgnore]
            public virtual int N 
            {
                get
                {
                    return B * P + A;
                }
                set
                {
                }
            }
            /// <summary> Fractional Division Value. Дробный делитель. </summary>
            [XmlIgnore]
            public virtual int Frac 
            {
                get
                {
                    return 1;
                }
                set
                {
                }
            }
            /// <summary> Modulus Value (M). </summary>
            [XmlIgnore]
            public virtual int Mod 
            {
                get
                {
                    return 1;
                }
                set
                {
                }
            }
            /// <summary> Делитель частоты ГУНа. </summary>
            [XmlIgnore]
            public virtual double VcoDiv 
            {
                get
                {
                    return 1.0F;
                }
                set
                {
                }
            }
            /// <summary> Частота ГУНа. </summary>
            [XmlIgnore]
            public virtual double VcoFrequency 
            {
                get
                {
                    double _n = (double)N + (double)((double)Frac / (double)Mod);
                    return (PFD * _n);
                }
                set
                {
                    double dN = value / PFD;
                    double diffN = 0.0F;
                    N = (int)Math.Floor(dN);
                    if (dN < N) N--;
                    diffN = dN - (double)N;
                    Frac = (int)((int)(diffN * (double)Mod));//+ 1);
                }
            }
            /// <summary> Выходная частота. </summary>
            [XmlIgnore]
            public virtual double OutFrequency 
            {
                get
                {
                    if (VcoFrequency > HighVcoFrequency) VcoFrequency = HighVcoFrequency;
                    if (VcoFrequency < LowVcoFrequency) VcoFrequency = LowVcoFrequency;
                    return VcoFrequency / VcoDiv;
                }
                set
                {
                    double freq = value;
                    if (freq > HighFrequency) freq = HighFrequency;
                    if (freq < LowFrequency) freq = LowFrequency;
                    VcoDiv = defineVcoDiv(freq);
                    VcoFrequency = freq * (double)VcoDiv;
                }
            }
            /// <summary> Установка/Считывание выходной частоты СЧ в целочисленном режиме. </summary>
            [XmlIgnore]
            public virtual double OutFrequencyInt 
            {
                get
                {
                    double Div = 0.0;
                    double Fout = 0.0;
                    Fout = (double)(B * P);
                    Fout = Fout + A;
                    Div = (double)(RefFrequency / R);
                    Fout = (double)(Fout * Div);
                    return Fout;
                }
                set
                {
                    double freq = value;
                    double Fcheck = 0.0;
                    if (freq < LowFrequency) freq = LowFrequency;
                    if (freq > HighFrequency) freq = HighFrequency;
                    B = (int)Math.Floor((double)((freq * R) / (RefFrequency * P)));
                    Fcheck = (double)((B * 64 * RefFrequency) / R);
                    A = (int)(((freq - Fcheck) * R) / RefFrequency);
                }
            }

            /// <summary> Проверка частоты. </summary>
            public void CheckFrequency ( )
            {
                if (OutFrequencyInt < LowFrequency) OutFrequencyInt = LowFrequency;
                if (OutFrequencyInt > HighFrequency) OutFrequencyInt = HighFrequency;
            }
            /// <summary> Определение делителя частоты ГУН </summary>
            /// <param name="freq"> Частота на выходе прибора </param>
            /// <returns>Код делителя</returns>
            public virtual int defineVcoDiv ( double freq )
            {
                return 1;
            }

            /// <summary> Конструктор класса. </summary>
            public Synthesizer  ( )
            {
                RefFrequency    = 24e6;
                Name            = "Synthesizer";
            }
            /// <summary> Конструктор класса. </summary>
            public Synthesizer  ( double _RefFrequency=24e6, String _name="Synthesizer" ) 
            {
                RefFrequency    = _RefFrequency;
                Name            = _name;
            }
        }
    }
}