/**
 * @file:       SequenceCallFunction.cs
 * @author:     Kirillov A.V.
 * @date:       10.01.2021
 * @version:    1.0
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nsAlexKir
{
    using Sequences;

    public class SequenceCallFunction : CBaseSequence
    {
        /// <summary> Массив параметров которые можно передать в функцию. </summary>
        public object[] parameters;

        /// <summary> Конструктор класса. </summary>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        public SequenceCallFunction(dResultCallbackFunction _result_function, object[] _parameters)
            : base ( _result_function ) 
        {
            parameters = _parameters;
        }

        /// <summary> Функция последовательности </summary>
        /// <param name="status"> Статус функции. </param>
        /// <param name="data"> Входные данные. </param>
        /// <returns> Статус выполнения функции. </returns>
        public override StatusSequence SequenceFunc ( StatusSequence status, params object[] data )
        {
            return base.SequenceFunc ( StatusSequence.eFinish, parameters );
        }
    }
}
