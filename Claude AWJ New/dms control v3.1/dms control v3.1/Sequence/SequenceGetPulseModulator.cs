using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace nsAlexKir
{
    using Sequences;

    public class SequenceGetPulseModulator : CBaseSequence
    {
        /// <summary> Указатель на список команд на отправку. </summary>
        List<Command> ltCmd;

        /// <summary> Конструктор класса. </summary>
        /// <param name="_ltCmd"> Указатель на список команд </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        public SequenceGetPulseModulator ( List<Command> _ltCmd, dResultCallbackFunction _result_function )
            : base ( _result_function )
        {
            ltCmd   = _ltCmd;
        }

        /// <summary> Основная функция последовательности. </summary>
        /// <param name="status"> Статус выполнения функции. </param>
        /// <param name="data"> Объект данных. </param>
        /// <returns> Статус завершения функции. </returns>
        public override StatusSequence SequenceFunc(StatusSequence status, params object[] data)
        {
            if ( status==StatusSequence.eStart )
            {
                Command cmd = new Command ( eTYPE_COMMANDS.GET_PULSE_CTRL );
                lock ( ltCmd )
                {
                    ltCmd.Add ( cmd );
                }
            }
            else if ( status==StatusSequence.eContinue )
            {
                Command cmd = (Command)data[0];
                if ( cmd.Status==eSTATUS_COMMANDS.DONE )
                {
                    ushort ctrl      = cmd.getUShort (0);
                    float frequency  = cmd.getFloat  (2);
                    float duty_cycle = cmd.getFloat  (6);

                    return base.SequenceFunc ( StatusSequence.eFinish, cmd.Status, ctrl, frequency, duty_cycle );
                }
                else
                {
                    return base.SequenceFunc ( StatusSequence.eFinish, cmd.Status );
                }
            }
            return StatusSequence.eContinue;
        }
    }
}
