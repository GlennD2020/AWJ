/**
 *  @file:      SequenceGetDacAttenuator.cs
 *  @remark:    Функция считывания состояния аттенюатора.
 *  @author:    Kirillov A.V.
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

    public class SequenceGetDacAttenuator  : CBaseSequence
    {
        /// <summary> Указатель на внешний список. </summary>
        List<Command> ltCmd;

        UInt16 index;

        /// <summary> Конструктор класса. </summary>
        /// <param name="_port"> Указатель на последовательный порт. </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        public SequenceGetDacAttenuator ( List<Command> _ltCmd, dResultCallbackFunction _result_function, UInt16 _index )
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
            if ( status==StatusSequence.eStart )
            {
                Command cmd = new Command ( eTYPE_COMMANDS.GET_DAC_VALUE );
                cmd.LenData = 2;
                cmd.setUShort ( index, 0 );                
                lock ( ltCmd )
                {
                    ltCmd.Add ( cmd );
                }
            }
            else if ( status==StatusSequence.eContinue )
            {
                Command cmd = (Command)data[0];
                if (cmd.Status == eSTATUS_COMMANDS.DONE)
                {
                    return base.SequenceFunc ( StatusSequence.eFinish, cmd.Status, cmd.getUShort(0), cmd.getUShort(2) );
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
