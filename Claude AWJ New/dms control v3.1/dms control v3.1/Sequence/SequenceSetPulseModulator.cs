using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace nsAlexKir
{
    using Sequences;

    public class SequenceSetPulseModulator : CBaseSequence
    {
        /// <summary> Последовательный порт. </summary>
        List<Command> ltCmd = null;

        /// <summary> </summary>
        UInt16 ctrl;
        /// <summary> </summary>
        float frequency;
        /// <summary> </summary>
        float duty_cycle;

        /// <summary> Конструктор класса. </summary>
        /// <param name="_port"> Указатель на последовательный порт. </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        public SequenceSetPulseModulator ( List<Command> _ltCmd, dResultCallbackFunction _result_function, UInt16 _ctrl, float _frequency, float _duty_cycle )
            : base ( _result_function )
        {
            ltCmd       = _ltCmd;

            ctrl        = _ctrl;
            frequency   = _frequency;
            duty_cycle  = _duty_cycle;
        }

        /// <summary> Основная функция последовательности. </summary>
        /// <param name="status"> Статус выполнения функции. </param>
        /// <param name="data"> Объект данных. </param>
        /// <returns> Статус завершения функции. </returns>
        public override StatusSequence SequenceFunc ( StatusSequence status, params object[] data )
        {
            if ( status==StatusSequence.eStart )
            {
                Command cmd = new Command ( eTYPE_COMMANDS.SET_PULSE_CTRL );
                cmd.LenData = 10;
                cmd.setUShort ( ctrl, 0 );
                cmd.setFloat  ( frequency, 2 );
                cmd.setFloat  ( duty_cycle, 6 );
                lock ( ltCmd )
                {
                    ltCmd.Add ( cmd );
                }
            }
            else if ( status==StatusSequence.eContinue )
            {
                return base.SequenceFunc ( StatusSequence.eFinish, data );
            }
            return StatusSequence.eContinue;
        }
    }
}
