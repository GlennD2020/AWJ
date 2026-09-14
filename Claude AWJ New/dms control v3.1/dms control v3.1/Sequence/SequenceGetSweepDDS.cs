/**
 * @file:   SequenceGetSweepDDS.cs
 * @author: Kirillov A.V.
 * @date:   20/06/2021
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

    public class SequenceGetSweepDDS : CBaseSequence
    {
        /// <summary>Внешний список команд.</summary>
        List<Command> ltCmd;

        /// <summary> Конструктор класса. </summary>
        /// <param name="_port"> Указатель на последовательный порт. </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        public SequenceGetSweepDDS ( List<Command> _ltCmd, dResultCallbackFunction _result_function )
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
            if ( status==StatusSequence.eStart )
            {
                Command cmd = new Command ( eTYPE_COMMANDS.GET_SWEEP_DDS );
                cmd.LenData = 2;
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
                    return base.SequenceFunc ( StatusSequence.eFinish,
                                               cmd.Status,
                                               cmd.getUShort(0),   //  Status
                                               cmd.getFloat(2),    //  Freq Start
                                               cmd.getFloat(6),    //  Step
                                               cmd.getULong(10));  //  Points
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
