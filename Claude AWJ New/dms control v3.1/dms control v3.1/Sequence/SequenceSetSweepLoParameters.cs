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
    public class SequenceSetSweepLoParameters : CBaseSequence
    {
        /// <summary> Количество точек в росчерке. </summary>
        ulong points = 1;
        /// <summary> Начальная частота. </summary>
        float BeginFreq = 2.4e9F;         
        /// <summary> Шаг частоты. </summary>
        float StepFreq = 0.0F;
        /// <summary> Время удержания частоты </summary>
        float HoldTime = 0.0F;
        /// <summary> Ожидание захвата. </summary>
        int   waitLD = 0;
        /// <summary> Управление росчерком. </summary>
        int   sweepOn = 0;
        /// <summary>Внешний список команд.</summary>
        List<Command> ltCmd;

        /// <summary> Конструктол класса. </summary>
        /// <param name="_port"> Последовательный Порт. </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        /// <param name="_points">    Количество точек росчерка. </param>
        /// <param name="_BeginFreq"> Начальная частота. </param>
        /// <param name="_StepFreq">  Шаг изменения частоты. </param>
        /// <param name="_HoldTime">  Время удержания частоты. </param>
        /// <param name="_waitLD">    Флаг ожидания захвата. </param>
        /// <param name="_SweepOn">   Управление росчерком. </param>
        public SequenceSetSweepLoParameters ( List<Command> _ltCmd, dResultCallbackFunction _result_function, 
            float _BeginFreq, float _StepFreq, ulong _points, float _HoldTime, int _waitLD, int _SweepOn )
            : base ( _result_function )
        {
            ltCmd     = _ltCmd;
            points    = _points < 1 ? 1 : _points;
            BeginFreq = _BeginFreq;
            StepFreq  = _StepFreq;
            HoldTime  = _HoldTime;
            waitLD    = _waitLD;
            sweepOn   = _SweepOn;
        }

        /// <summary> Основная функция последовательности. </summary>
        /// <param name="status">   Статус выполнения функции. </param>
        /// <param name="data">     Объект данных. </param>
        /// <returns>   Статус завершения функции. </returns>
        public override StatusSequence SequenceFunc ( StatusSequence status, params object[] data )
        {
            if ( status == StatusSequence.eStart )
            {
                Command cmd = new Command ( eTYPE_COMMANDS.SET_SWEEP_PLL );
                cmd.LenData = 18;
                cmd.setFloat ( BeginFreq,      0 );
                cmd.setFloat ( StepFreq,       4 );
                cmd.setULong ( points,         8 );
                cmd.setFloat ( HoldTime,      12 );
                cmd.setByte  ( (byte)waitLD,  16 );
                cmd.setByte  ( (byte)sweepOn, 17 );
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
