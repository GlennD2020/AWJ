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

    public class SequenceReadRegisterPLL : CBaseSequence
    {
        /// <summary> Индекс регистра. </summary>
        short index;
        /// <summary> Последовательный порт. </summary>
        List<Command> ltCmd = null;

        /// <summary> Конструктор класса. </summary>
        /// <param name="_port"> Указатель на последовательный порт. </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        public SequenceReadRegisterPLL ( List<Command> _ltCmd, dResultCallbackFunction _result_function, short _index )
            : base ( _result_function )
        {
            ltCmd = _ltCmd;
            index = _index;
        }

        /// <summary> Основная функция последовательности. </summary>
        /// <param name="status"> Статус выполнения функции. </param>
        /// <param name="data"> Объект данных. </param>
        /// <returns> Статус завершения функции. </returns>
        public override StatusSequence SequenceFunc ( StatusSequence status, params object[] data )
        {
            if (status == StatusSequence.eStart)
            {
                Command cmd = new Command ( eTYPE_COMMANDS.GET_PLL_REG );
                cmd.LenData = 2;
                cmd.setShort ( index, 0 );
                lock ( ltCmd )
                {
                    ltCmd.Add ( cmd );
                }
            }
            else if (status == StatusSequence.eContinue)
            {
                Command cmd  = (Command)data[0];
                int register = cmd.getInt(0);
                int address  = (int)(register & 0x07);

                return base.SequenceFunc ( StatusSequence.eFinish, cmd.Status, address, register );
            }
            return StatusSequence.eContinue;
        }
    }
}
