/**
 * @file:   SequenceGetSweepLoParameters.cs
 * @author: Kirillov A.V.
 * @date:   19/04/2020
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

    public class SequenceGetSweepLoParameters : CBaseSequence
    {
        /// <summary>Внешний список команд.</summary>
        List<Command> ltCmd;

        /// <summary> Конструктор класса. </summary>
        /// <param name="_port"> Указатель на последовательный порт. </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        public SequenceGetSweepLoParameters ( List<Command> _ltCmd, dResultCallbackFunction _result_function )
            : base ( _result_function )
        {
            ltCmd = _ltCmd;
        }

        /// <summary> Основная функция последовательности. </summary>
        /// <param name="status"> Статус выполнения функции. </param>
        /// <param name="data"> Объект данных. </param>
        /// <returns> Статус завершения функции. </returns>
        public override StatusSequence SequenceFunc ( StatusSequence status, params object[] data )
        {
            if (status == StatusSequence.eStart)
            {
                Command cmd = new Command(eTYPE_COMMANDS.GET_SWEEP_PLL);
                cmd.LenData = 2;
                lock ( ltCmd )
                {
                    ltCmd.Add ( cmd );
                }
            }
            else if (status == StatusSequence.eContinue)
            {
                Command cmd = (Command)data[0];
                if (cmd.Status == eSTATUS_COMMANDS.DONE)
                {
                    return base.SequenceFunc ( StatusSequence.eFinish, cmd.Status, 
                        cmd.getFloat(0), cmd.getFloat(4), (uint)cmd.getULong(8), cmd.getFloat(12), cmd.getByte(16), cmd.getByte(17) );
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
