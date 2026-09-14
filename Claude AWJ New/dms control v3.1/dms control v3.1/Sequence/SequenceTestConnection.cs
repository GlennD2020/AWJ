/**
 * Файл:    SequenceTestConnection.cs
 * Автор:   Кириллов А.В.
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

    /// <summary> Последовательность тестирования соединения.</summary>
    public class SequenceTestConnection : CBaseSequence
    {
        /// <summary> Указатель на список команд на отправку. </summary>
        List<Command> ltCmd;

        /// <summary> Конструктор класса </summary>
        /// <param name="_ltCmd"> Указатель на список команд </param>
        /// <param name="_result_function"> Функция обратного вызова </param>
        public SequenceTestConnection ( List<Command> _ltCmd, dResultCallbackFunction _result_function )
            : base(_result_function)
        {
            ltCmd = _ltCmd;
        }

        /// <summary> Функция последовательности </summary>
        /// <param name="status"> Статус функции. </param>
        /// <param name="data"> Объект данных. </param>
        /// <returns> Статус выполнения функции. </returns>
        public override StatusSequence SequenceFunc ( StatusSequence status, params object[] data )
        {
            if ( status==StatusSequence.eStart )
            {
                lock (ltCmd)
                {
                    ltCmd.Add ( new Command ( eTYPE_COMMANDS.TEST_CONNECT ) );
                }
            }
            else if ( status==StatusSequence.eContinue )
            {
                Command cmd = (Command)data[0];
                int result = ( cmd.Status==eSTATUS_COMMANDS.DONE ) ? 1 : 0;
                return base.SequenceFunc ( StatusSequence.eFinish, result );
            }
            return StatusSequence.eContinue;
        }

    }
}
