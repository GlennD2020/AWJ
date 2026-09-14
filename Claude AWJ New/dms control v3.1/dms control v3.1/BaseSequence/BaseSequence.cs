using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nsAlexKir
{
namespace Sequences
{
    /// <summary>Статусы последовательностей</summary>
    public enum StatusSequence : int
    {
        /// <summary> Запуск функциональной последовательности. </summary>
        eStart = 0,
        /// <summary> Продолжение функциональной последовательности. </summary>
        eContinue,
        /// <summary> Остановка функциональной последовательности. </summary>
        eStop,
        /// <summary> Последовательность завершена. </summary>
        eFinish,
    }
    /// <summary> Интерфейс функциональной последовательности. </summary>
    public interface IBaseSequence
    {
        /// <summary> Испольняемая функция. </summary>
        /// <param name="status"> Статус операции. </param>
        /// <param name="data"> Данные необходимые для выполнения какой то операции. </param>
        /// <returns> Статус выполнения операции. </returns>
        StatusSequence SequenceFunc ( StatusSequence status, params object[] data );
        /// <summary> Функция считывания статуса операции. </summary>
        /// <returns> Текущий статус операции. </returns>
        bool InProgress ( );
        /// <summary> Функция добавления данных в процессе выполнения</summary>
        /// <param name="source">ID источника добавляемых данных. </param>
        /// <param name="data"> Добавляемые данные. </param>
        /// <remarks> Функция может использоваться для добавление в функциональный процесс принятых данных. </remarks>
        void AppendData ( ConnectionID source, object data );
    }
    /// <summary> Базовый класс элемента последовательности. </summary>
    public class CBaseSequence : IBaseSequence
    {
        /// <summary> Статус элемента последовательности. </summary>
        public bool bActionStatus;
        /// <summary> Флаг сигнализирующий о принятии данных. </summary>
        public bool bReceivedData;
        /// <summary> Конструктор базового класса. </summary>
        public CBaseSequence ( ) 
        {
            bActionStatus = false;
            bReceivedData = false;
        }
        /// <summary> Конструктор базового класса. </summary>
        public CBaseSequence ( dResultCallbackFunction result_function ) 
        {
            bActionStatus = false;
            bReceivedData = false;
            ResultFunction = result_function;
        }
        /// <summary> Считывание статуса </summary>
        /// <returns> Статус элемента последовательности. </returns>
        public bool InProgress() { return bActionStatus; }
        /// <summary> Исполняемая функция элемента цепочки. </summary>
        /// <param name="status"> Статус выполнения функции. </param>
        /// <param name="data"> Данные передаваемые в функцию. </param>
        /// <returns> Статус выполнения функции. </returns>
        public virtual StatusSequence SequenceFunc ( StatusSequence status, params object[] data ) 
        {
            if ( status==StatusSequence.eFinish )
            {
                if ( ResultFunction!=null )
                {
                    ResultFunction(data);
                }
            }
            return StatusSequence.eFinish;
        }
        /// <summary> Функция добавления данных в процессе выполнения</summary>
        /// <param name="source">ID источника добавляемых данных. </param>
        /// <param name="data"> Добавляемые данные. </param>
        /// <remarks> Функция может использоваться для добавление в функциональный процесс принятых данных. </remarks>
        public virtual void AppendData ( ConnectionID source, object data ) 
        { 
            bReceivedData = true; 
        }
        /// <summary> Функция вызываемая при завершении процедуры. Функция должна быть потоко безопасной.</summary>
        /// <param name="data"> Данные </param>
        public delegate void dResultCallbackFunction ( params object[] data );
        /// <summary> Указатель на функцию которую нужно вызвать при завершении процедуры. </summary>
        public dResultCallbackFunction ResultFunction;
    }
}   //  Sequences
}   //  nsAlexKir
