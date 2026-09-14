/**
 *  @file:      SequenceConnection.cs
 *  @author:    Kirillov A.V.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nsAlexKir
{
namespace Sequences
{
    /// <summary> Перечисление доступных ID соединений. </summary>
    public enum eConnectionID
    {
        /// <summary> Сокет. </summary>
        TCP_IP = 1,
        /// <summary> USB соединение. </summary>
        USB,
        /// <summary> Последовательный порт. </summary>
        SerialPort,
    }

    /// <summary> Интерфейс класса ID соединения. </summary>
    public interface IConnectionID
    {
        /// <summary> Функция получения ID соединения. </summary>
        /// <returns> ID соединения. </returns>
        eConnectionID getID ( );
    }

    /// <summary> Класс ID соединения. </summary>
    public class ConnectionID
    {
        /// <summary> Идентификатор соединения. </summary>
        eConnectionID id_connection;
        
        /// <summary> Конструктор класса. </summary>
        /// <param name="id"> ID соединения </param>
        public ConnectionID ( eConnectionID id )
        {
            id_connection = id;
        }

        /// <summary> Получение ID соединения. </summary>
        /// <returns> ID соединения. </returns>
        public eConnectionID getID ( )
        { 
            return id_connection; 
        }
    }
    
    /// <summary> Подключение последовательности. </summary>
    public class SequenceConnection
    {
        /// <summary> Конструктор класса. </summary>
        public SequenceConnection ( )
        {
        }
    }
}   //  Sequences
}   //  nsAlexKir
