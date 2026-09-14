using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace nsAlexKir
{
    using Sequences;

    public class SequenceSetDDSCtrlFrequency : CBaseSequence
    {
        /// <summary> Последовательный порт. </summary>
        List<Command> ltCmd = null;

        /// <summary> Частота линии CTRL. </summary>
        float frequency;

        /// <summary> Конструктор класса. </summary>
        /// <param name="_port"> Указатель на последовательный порт. </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        public SequenceSetDDSCtrlFrequency ( List<Command> _ltCmd, dResultCallbackFunction _result_function, float _frequency )
            : base ( _result_function )
        {
            ltCmd     = _ltCmd;
            frequency = _frequency;
        }

        /// <summary> Основная функция последовательности. </summary>
        /// <param name="status"> Статус выполнения функции. </param>
        /// <param name="data"> Объект данных. </param>
        /// <returns> Статус завершения функции. </returns>
        public override StatusSequence SequenceFunc ( StatusSequence status, params object[] data )
        {
            if ( status==StatusSequence.eStart )
            {
                Command cmd = new Command ( eTYPE_COMMANDS.SET_DDS_CTR_FREQ );
                cmd.LenData = 4;
                cmd.setFloat ( frequency, 0 );
                lock ( ltCmd )
                {
                    ltCmd.Add ( cmd );
                }
            }
            else if ( status==StatusSequence.eContinue )
            {
                return base.SequenceFunc ( StatusSequence.eFinish, data );
            }
            return StatusSequence.eContinue;
        }
    }
}
