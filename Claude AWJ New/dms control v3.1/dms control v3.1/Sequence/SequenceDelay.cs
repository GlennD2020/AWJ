/**
 * @file:       SequenceReadMemory.cs
 * @project:    Элемент задержки выполнения последовательности.
 * @author:     Kirillov A.V.
 * @date:       11/04/2020
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Ports;

namespace nsAlexKir
{
    using Sequences;

    /// <summary> Задержка выполнения последовательности. </summary>
    public class SequenceDelay : CBaseSequence
    {
        /// <summary> Величина задержки. </summary>
        int delay = 0;

        /// <summary> Конструктор класса.</summary>
        /// <param name="_result_function"> Указатель на функцию, которая должна быть вызвана при завершении процесса. </param>
        /// <param name="_delay"> Величина задержки. </param>
        public SequenceDelay ( int _delay, dResultCallbackFunction _result_function=null ) 
            : base ( _result_function )
        {
            delay = _delay;
        }

        /// <summary> Основная функция последовательности. </summary>
        /// <param name="status"> Статус выполнения функции. </param>
        /// <param name="data"> Объект данных. </param>
        /// <returns> Статус завершения функции. </returns>
        public override StatusSequence SequenceFunc ( StatusSequence status, params object[] data )
        {
            Thread.Sleep ( delay );
            return base.SequenceFunc ( StatusSequence.eFinish, null );
        }
    }
}
