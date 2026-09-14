using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace nsAlexKir
{
    using Sequences;

    public class SequenceSetDacAttenuator : CBaseSequence
    {
        /// <summary> Указатель на внешний список. </summary>
        List<Command> ltCmd;
        /// <summary> Код ЦАП. </summary>
        ushort dac = 0;
        /// <summary> Флаг управления аттенюатором. </summary>
        ushort att_ctrl = 0;

        /// <summary> Конструктор класса. </summary>
        /// <param name="_port"> Указатель на последовательный порт. </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        /// <param name="_dac"> Код ЦАП </param>
        /// <param name="_att_ctrl"> Флаг управления аттенюатора. </param>
        public SequenceSetDacAttenuator ( List<Command> _ltCmd, dResultCallbackFunction _result_function, ushort _dac, ushort _att_ctrl )
            : base(_result_function)
        {
            ltCmd    = _ltCmd;
            dac      = _dac;
            att_ctrl = _att_ctrl;
        }

        /// <summary> Основная функция последовательности. </summary>
        /// <param name="status"> Статус выполнения функции. </param>
        /// <param name="data"> Объект данных. </param>
        /// <returns> Статус завершения функции. </returns>
        public override StatusSequence SequenceFunc(StatusSequence status, params object[] data)
        {
            if (status == StatusSequence.eStart)
            {
                Command cmd = new Command ( eTYPE_COMMANDS.SET_DAC_VALUE );
                cmd.LenData = 4;
                cmd.setUShort ( att_ctrl, 0 );
                cmd.setUShort ( dac, 2 );
                lock (ltCmd)
                {
                    ltCmd.Add(cmd);
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

