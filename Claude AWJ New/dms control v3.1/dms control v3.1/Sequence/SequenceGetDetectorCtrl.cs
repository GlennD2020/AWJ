using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace nsAlexKir
{
    using Sequences;

    public class SequenceGetDetectorCtrl : CBaseSequence
    {
        /// <summary> Указатель на список команд на отправку. </summary>
        List<Command> ltCmd;

        /// <summary> Конструктор класса. </summary>
        /// <param name="_ltCmd"> Указатель на список команд </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        public SequenceGetDetectorCtrl ( List<Command> _ltCmd, dResultCallbackFunction _result_function )
            : base ( _result_function )
        {
            ltCmd   = _ltCmd;
        }

        /// <summary> Основная функция последовательности. </summary>
        /// <param name="status"> Статус выполнения функции. </param>
        /// <param name="data"> Объект данных. </param>
        /// <returns> Статус завершения функции. </returns>
        public override StatusSequence SequenceFunc(StatusSequence status, params object[] data)
        {
            if ( status==StatusSequence.eStart )
            {
                Command cmd = new Command ( eTYPE_COMMANDS.GET_CTRL_POWER_DET );
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
                    ushort[] param = new ushort[3];
                    param [0] = cmd.getUShort(0);
                    param [1] = cmd.getUShort(2);
                    param [2] = cmd.getUShort(4);

                    double ReqVoltage = (((double)param[1]) * 3.3) / 4096.0;
                    double Voltage    = (((double)param[2]) * 3.3) / 4096.0;

                    return base.SequenceFunc ( StatusSequence.eFinish, cmd.Status, param[0], ReqVoltage, Voltage );
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
