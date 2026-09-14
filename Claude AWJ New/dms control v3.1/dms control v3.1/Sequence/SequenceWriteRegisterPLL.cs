using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace nsAlexKir
{
    using Sequences;

    /// <summary> Последовательность для записи регистра микросхемы ФАПЧ. </summary>
    public class SequenceWriteRegisterPLL : CBaseSequence
    {
        /// <summary> Последовательный порт. </summary>
        List<Command> ltCmd = null;

        /// <summary> Регистр. </summary>
        ulong register;

        /// <summary> Конструктор класса. </summary>
        /// <param name="_ltCmd"> </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        public SequenceWriteRegisterPLL ( List<Command> _ltCmd, dResultCallbackFunction _result_function, ulong _register )
            : base ( _result_function )
        {
            ltCmd    = _ltCmd;
            register = _register;
        }

        /// <summary> Основная функция последовательности. </summary>
        /// <param name="status"> Статус выполнения функции. </param>
        /// <param name="data"> Объект данных. </param>
        /// <returns> Статус завершения функции. </returns>
        public override StatusSequence SequenceFunc ( StatusSequence status, params object[] data )
        {
            if (status == StatusSequence.eStart)
            {
                Command cmd = new Command(eTYPE_COMMANDS.SET_PLL_REG);
                cmd.LenData = 4;
                cmd.setULong ( register, 0 );
                lock ( ltCmd )
                {
                    ltCmd.Add ( cmd );
                }
            }
            else if (status == StatusSequence.eContinue)
            {
                return base.SequenceFunc ( StatusSequence.eFinish, data );
            }
            return StatusSequence.eContinue;
        }
    }
}
