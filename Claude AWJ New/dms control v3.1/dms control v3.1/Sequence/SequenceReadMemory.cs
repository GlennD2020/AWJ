/**
 * @file:       SequenceReadMemory.cs
 * @project:    "Последовательное действие" считывание из памяти
 * @author:     Kirillov A.V.
 * @date:       12/09/2021
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

    public class SequenceReadMemory : CBaseSequence
    {
        /// <summary> Последовательный порт. </summary>
        List<Command> ltCmd = null;
        /// <summary> Массив считанных данных. </summary>
        byte[] read_data = null;
        /// <summary> Адрес для считывания. </summary>
        ushort address = 0;
        /// <summary> Количество вычитываемых данных. </summary>
        ushort num_data = 0;
        /// <summary> Смещение для вычитывания данных в несколько шагов. </summary>
        ushort offset = 0;
        /// <summary> Счетчик считанных данных.</summary>
        ushort counter = 0;

        /// <summary> Конструктор класса. </summary>
        /// <param name="_ltCmd">  </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        public SequenceReadMemory ( List<Command> _ltCmd, dResultCallbackFunction _result_function, ushort _address, ushort _num_data )
            : base ( _result_function )
        {
            ltCmd     = _ltCmd;
            address   = _address;
            num_data  = _num_data;
            offset    = 0;
            read_data = new byte[num_data];
        }

        /// <summary> Конструктор класса. </summary>
        /// <param name="_ltCmd"> </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        public SequenceReadMemory ( List<Command> _ltCmd, dResultCallbackFunction _result_function, ushort _address, ushort _num_data, ushort _offset )
            : base ( _result_function )
        {
            ltCmd     = _ltCmd;
            address   = _address;
            num_data  = _num_data;
            offset    = _offset;
            read_data = new byte[num_data];
        }


        /// <summary> Основная функция последовательности. </summary>
        /// <param name="status"> Статус выполнения функции. </param>
        /// <param name="data"> Объект данных. </param>
        /// <returns> Статус завершения функции. </returns>
        public override StatusSequence SequenceFunc ( StatusSequence status, params object[] data )
        {
            Command cmd = new Command ( eTYPE_COMMANDS.READ_MEMORY );

            cmd.LenData = 4;

            if ( status==StatusSequence.eStart )
            {
                counter = (ushort)(( num_data>22 ) ? 22 : num_data);
                cmd.setUShort ( address, 0 );
                cmd.setUShort ( counter, 2 );
                lock ( ltCmd )
                {
                    ltCmd.Add ( cmd );
                }
            }
            else if ( status==StatusSequence.eContinue )
            {
                for ( int i=0; i<counter; i++ )
                {
                    read_data[offset + i] = (byte)(((Command)data[0]).body[Command.HEAD_SIZE+4+i]);
                }
                offset += counter;
                if (offset < num_data)
                {
                    counter = (ushort)(((num_data - offset) > 22) ? 22 : num_data - offset);
                    cmd.setUShort ( (ushort)(address+offset), 0 );
                    cmd.setUShort ( counter, 2 );
                    lock ( ltCmd )
                    {
                        ltCmd.Add ( cmd );
                    }                    
                }
                else
                {
                    return base.SequenceFunc ( StatusSequence.eFinish, read_data, address, num_data );
                }
            }
            return StatusSequence.eContinue;
        }
    }
}
