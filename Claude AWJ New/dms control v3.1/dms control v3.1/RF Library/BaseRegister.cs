using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace nsAlexKir
{
    /// <summary> Базовый класс регистра. </summary>
    /// <typeparam name="T"> Тип регистра. </typeparam>
    [Serializable]
    public class BaseRegister<T>
    {
        /// <summary> Массив данных. </summary>
        [XmlIgnore]
        public byte[] data;

        /// <summary> Адрес регистра. </summary>
        [XmlIgnore]
        public virtual int address 
        {
            get;
            set;
        }

        /// <summary> Значение регистра. </summary>
        [XmlIgnore]
        public virtual T value
        {
            get;
            set;
        }

        /// <summary> Тело регистра. </summary>
        [XmlElement("body")]
        public virtual T body 
        {
            get;
            set;
        }

        /// <summary> Имя регистра.</summary>
        [XmlElement("name")]
        public virtual String Name
        {
            get
            {
                return String.Format("Register {0:d}", address);
            }
            set
            {
            }
        }

        /// <summary> Конструктор класса. </summary>
        /// <param name="value"> Размер регистра в байтах. </param>
        public BaseRegister ( int size_register ) 
        {
            data = new byte[size_register];
        }

        /// <summary> Конструктор класса. </summary>
        public BaseRegister ( )
        {
            data = new byte[4];
        }
    }
}
