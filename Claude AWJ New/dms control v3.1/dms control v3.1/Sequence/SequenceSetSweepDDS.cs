/*
 *  Project:    DDS Mixer Synthesizer. 
 *  File:       SequenceSetSweepLoParameters.cs
 *  Author:     Kirillov A.V.
 *  Date:       28.02.2020
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace nsAlexKir
{
    using Sequences;

    /// <summary> Последовательность считывания настроек параметров свипа ФАПЧ. </summary>
    public class SequenceSetSweepDDS : CBaseSequence
    {
        /// <summary>Внешний список команд.</summary>
        List<Command> ltCmd;
        /// <summary> Управление росчерком. </summary>
        ushort  type_sweep = 0;
        /// <summary> </summary>
        float   start   = 100000;
        /// <summary> Шаг по частоте. </summary>
        float   step    = 100000;
        /// <summary> Кол-во точек. </summary>
        ulong   points  = 11;

        /// <summary> Конструктол класса. </summary>
        /// <param name="_port"> Последовательный Порт. </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        /// <param name="_type_sweep"> Тип росчерка ДДС. </param>
        /// <param name="_points"> Количество точек росчерка. </param>
        /// <param name="_step">  Шаг изменения частоты. </param>
        public SequenceSetSweepDDS ( List<Command> _ltCmd, dResultCallbackFunction _result_function, ushort _type_sweep, float _start, float _step, ulong _points )
            : base ( _result_function )
        {
            ltCmd       = _ltCmd;
            type_sweep  = _type_sweep;
            start       = _start;
            step        = _step;
            points      = _points;
        }

        /// <summary> Основная функция последовательности. </summary>
        /// <param name="status">   Статус выполнения функции. </param>
        /// <param name="data">     Объект данных. </param>
        /// <returns>   Статус завершения функции. </returns>
        public override StatusSequence SequenceFunc ( StatusSequence status, params object[] data )
        {
            if ( status == StatusSequence.eStart )
            {
                Command cmd = new Command ( eTYPE_COMMANDS.SET_SWEEP_DDS );
                cmd.LenData = 2+4+4+4;
                cmd.setUShort ( (ushort)type_sweep, 0 );
                cmd.setFloat  ( start,  2 );
                cmd.setFloat  ( step,   6 );
                cmd.setULong  ( points, 10 );
                lock ( ltCmd )
                {
                    ltCmd.Add ( cmd );
                }
            }
            else if ( status == StatusSequence.eContinue )
            {
                return base.SequenceFunc ( StatusSequence.eFinish, data );
            }
            return StatusSequence.eContinue;
        }
    }
}
