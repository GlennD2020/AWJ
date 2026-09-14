/**
 * File:    SequenceSetDetectorCtrl.cs
 * Project: DDS Mixer Synthesizer
 * Author:  Kirillov A.V.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;

namespace nsAlexKir
{
    using Sequences;

    public class SequenceSetDetectorCtrl : CBaseSequence
    {
        /// <summary> Указатель на список команд на отправку. </summary>
        List<Command> ltCmd;
        /// <summary> Управление статусом контроля напряжения. </summary>
        ushort ctrl = 0;
        /// <summary> Предельное значение АЦП, при достижении которого должно произойти звуковое оповещение. </summary>
        ushort limit_adc = 0;

        /// <summary> Конструктор класса. </summary>
        /// <param name="_ltCmd"> Указатель на список команд </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        public SequenceSetDetectorCtrl ( List<Command> _ltCmd, ushort _ctrl, double _LimitVoltage, dResultCallbackFunction _result_function )
            : base ( _result_function )
        {
            ltCmd     = _ltCmd;
            ctrl      = _ctrl;
            limit_adc = (ushort)((_LimitVoltage * 4096.0) / 3.3);
        }

        /// <summary> Конструктор класса. </summary>
        /// <param name="_ltCmd"> Указатель на список команд. </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        public SequenceSetDetectorCtrl ( List<Command> _ltCmd, ushort _ctrl, ushort _limit_adc, dResultCallbackFunction _result_function )
            : base ( _result_function )
        {
            ltCmd     = _ltCmd;
            ctrl      = _ctrl;
            limit_adc = _limit_adc;
        }

        /// <summary> Основная функция последовательности. </summary>
        /// <param name="status"> Статус выполнения функции. </param>
        /// <param name="data"> Объект данных. </param>
        /// <returns> Статус завершения функции. </returns>
        public override StatusSequence SequenceFunc ( StatusSequence status, params object[] data )
        {
            if ( status==StatusSequence.eStart )
            {
                Command cmd = new Command ( eTYPE_COMMANDS.SET_CTRL_POWER_DET );
                cmd.LenData = 4;
                cmd.setUShort ( ctrl,      0 );
                cmd.setUShort ( limit_adc, 2 );
                lock ( ltCmd )
                {
                    ltCmd.Add ( cmd );
                }
            }
            else if ( status==StatusSequence.eContinue )
            {
                return base.SequenceFunc(StatusSequence.eFinish, ((Command)data[0]).Status);
            }
            return StatusSequence.eContinue;
        }
    }
}
