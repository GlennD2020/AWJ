/**
 * File:    SequenceReadRegisterPLL.cs
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

    public class SequenceResetDDS : CBaseSequence
    {
        /// <summary> Последовательный порт. </summary>
        List<Command> ltCmd = null;

        /// <summary> Конструктор класса. </summary>
        /// <param name="_port"> Указатель на последовательный порт. </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        /// <param name="_address"> Номер регистра </param>
        /// <param name="_register"></param>
        public SequenceResetDDS ( List<Command> _ltCmd, dResultCallbackFunction _result_function )
            : base ( _result_function )
        {
            ltCmd       = _ltCmd;
        }

        /// <summary> Основная функция последовательности. </summary>
        /// <param name="status"> Статус выполнения функции. </param>
        /// <param name="data"> Объект данных. </param>
        /// <returns> Статус завершения функции. </returns>
        public override StatusSequence SequenceFunc ( StatusSequence status, params object[] data )
        {
            if ( status==StatusSequence.eStart )
            {
                Command cmd = new Command ( eTYPE_COMMANDS.RESET_DDS );
                lock ( ltCmd )
                {
                    ltCmd.Add ( cmd );
                }
            }
            else if ( status==StatusSequence.eContinue )
            {
                Command cmd  = (Command)data[0];
                return base.SequenceFunc ( StatusSequence.eFinish, cmd.Status );
            }
            return StatusSequence.eContinue;
        }
    }
}
