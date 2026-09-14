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
    public class SequenceSetRampDDS : CBaseSequence
    {
        /// <summary>Внешний список команд.</summary>
        List<Command> ltCmd;
        /// <summary> Управление росчерком. </summary>
        ushort  MaxRamp = 0;
        /// <summary> </summary>
        ushort  Points   = 101;

        /// <summary> Конструктол класса. </summary>
        /// <param name="_port"> Последовательный Порт. </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        /// <param name="_SweepOn"> Управление росчерком. </param>
        /// <param name="_points"> Количество точек росчерка. </param>
        /// <param name="_step">  Шаг изменения частоты. </param>
        public SequenceSetRampDDS ( List<Command> _ltCmd, dResultCallbackFunction _result_function, ushort _maxRamp, ushort _points )
            : base ( _result_function )
        {
            ltCmd    = _ltCmd;
            MaxRamp  = _maxRamp;
            Points   = _points;
        }

        /// <summary> Основная функция последовательности. </summary>
        /// <param name="status">   Статус выполнения функции. </param>
        /// <param name="data">     Объект данных. </param>
        /// <returns>   Статус завершения функции. </returns>
        public override StatusSequence SequenceFunc ( StatusSequence status, params object[] data )
        {
            if ( status == StatusSequence.eStart )
            {
                Command cmd = new Command ( eTYPE_COMMANDS.SET_RAMP_DDS );
                cmd.LenData = 4;
                cmd.setUShort ( (ushort)MaxRamp,    0 );
                cmd.setUShort ( (ushort)Points,     2 );
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
