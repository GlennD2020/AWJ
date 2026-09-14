/**
 * File:    SequenceDefaultState.cs
 * Remark:  Set the device to default state.
 * Author:  Kirillov A.V.
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

    public class SequenceDefaultState : CBaseSequence
    {
        /// <summary> Последовательный порт. </summary>
        List<Command> ltCmd = null;

        /// <summary> Конструктор класса. </summary>
        /// <param name="_port"> Указатель на последовательный порт. </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        public SequenceDefaultState ( List<Command> _ltCmd, dResultCallbackFunction _result_function )
            : base ( _result_function )
        {
            ltCmd  = _ltCmd;
        }

        /// <summary> Основная функция последовательности. </summary>
        /// <param name="status"> Статус выполнения функции. </param>
        /// <param name="data"> Объект данных. </param>
        /// <returns> Статус завершения функции. </returns>
        public override StatusSequence SequenceFunc ( StatusSequence status, params object[] data )
        {
            if (status == StatusSequence.eStart)
            {
                Command cmd = new Command ( eTYPE_COMMANDS.DEFAULT );
                cmd.LenData = 0;
                lock ( ltCmd )
                {
                    ltCmd.Add ( cmd );
                }
            }
            else if (status == StatusSequence.eContinue)
            {
                return base.SequenceFunc(StatusSequence.eFinish, data);
            }
            return StatusSequence.eContinue;
        }
    }
}
