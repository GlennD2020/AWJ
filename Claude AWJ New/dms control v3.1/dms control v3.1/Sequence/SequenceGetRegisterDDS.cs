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

    public class SequenceGetRegisterDDS : CBaseSequence
    {
        /// <summary> Последовательный порт. </summary>
        List<Command> ltCmd = null;
        /// <summary> Индекс регистра. </summary>
        ushort index;


        /// <summary> Конструктор класса. </summary>
        /// <param name="_port"> Указатель на последовательный порт. </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        public SequenceGetRegisterDDS ( List<Command> _ltCmd, dResultCallbackFunction _result_function, ushort _index )
            : base ( _result_function )
        {
            ltCmd   = _ltCmd;
            index   = _index;
        }

        /// <summary> Основная функция последовательности. </summary>
        /// <param name="status"> Статус выполнения функции. </param>
        /// <param name="data"> Объект данных. </param>
        /// <returns> Статус завершения функции. </returns>
        public override StatusSequence SequenceFunc ( StatusSequence status, params object[] data )
        {
            if ( status==StatusSequence.eStart )
            {
                Command cmd = new Command ( eTYPE_COMMANDS.GET_DDS_REG );
                cmd.LenData = 2;
                cmd.setUShort ( index, 0 );
                lock ( ltCmd )
                {
                    ltCmd.Add ( cmd );
                }
            }
            else if ( status==StatusSequence.eContinue )
            {
                Command cmd     = (Command)data[0];                
                ushort address  = cmd.getUShort(0);
                ushort register = cmd.getUShort(2);
                return base.SequenceFunc ( StatusSequence.eFinish, cmd.Status, address, register );
            }
            return StatusSequence.eContinue;
        }
    }
}
