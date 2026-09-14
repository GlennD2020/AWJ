/**
 * @file:       SequenceWriteMemory.cs
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

    /// <summary> "Последовательное действие" для записи в память. </summary>
    public class SequenceWriteMemory : CBaseSequence
    {
        /// <summary> Последовательный порт. </summary>
        List<Command> ltCmd = null;
        /// <summary> Адрес для записи. </summary>
        ushort address = 0;
        /// <summary> Количество записываемых данных. </summary>
        ushort num_data = 0;
        /// <summary> Массив записываемых данных. </summary>
        byte[] write_data = null;
        /// <summary> Счетчик записанных данных.</summary>
        ushort counter = 0;
        /// <summary> Смещение. </summary>
        ushort offset = 0;

        /// <summary> Конструктор класса. </summary>
        /// <param name="_ltCmd"> Указатель на последовательный порт. </param>
        /// <param name="_result_function"> Функция обратного вызова. </param>
        public SequenceWriteMemory ( List<Command> _ltCmd, dResultCallbackFunction _result_function, ushort _address, 
            byte[] _data, ushort _num_data )
            : base ( _result_function )
        {
            address     = _address;
            num_data    = _num_data;

            write_data  = new byte [ num_data ];
            for ( ushort i=0; i<num_data; i++ )
            {
                write_data[i] = _data[i + offset];
            }
            ltCmd    = _ltCmd;            
        }

        /// <summary> Основная функция последовательности. </summary>
        /// <param name="status"> Статус выполнения функции. </param>
        /// <param name="data"> Объект данных. </param>
        /// <returns> Статус завершения функции. </returns>
        public override StatusSequence SequenceFunc ( StatusSequence status, params object[] data ) 
        {
            if ( status==StatusSequence.eStart )
            {
                Command cmd = new Command ( eTYPE_COMMANDS.WRITE_MEMORY );
                counter = (ushort)(( num_data>22 ) ? 22 : num_data);
                cmd.setUShort ( address, 0 );
                cmd.setUShort ( counter, 2 );
                cmd.LenData = (short)( 4 + counter );
                for ( ushort i=0; i<counter; i++ )
                {
                    cmd.setByte ( write_data[i], 4 + i );
                }
                lock ( ltCmd )
                {
                    ltCmd.Add ( cmd );
                }
                offset = counter;
            }
            else if ( status==StatusSequence.eContinue )
            {
                if (offset < num_data)
                {
                    Command cmd = new Command ( eTYPE_COMMANDS.WRITE_MEMORY );
                    counter = (ushort)(((num_data - offset) > 22) ? 22 : (num_data - offset));
                    cmd.setUShort ( (ushort)(address + offset), 0 );
                    cmd.setUShort ( counter, 2 );
                    cmd.LenData = (short)(4 + counter);
                    for ( ushort i=0; i<counter; i++ )
                    {
                        cmd.setByte(write_data[i + offset], 4 + i);
                    }
                    lock ( ltCmd )
                    {
                        ltCmd.Add ( cmd );
                    }
                    offset += counter;
                }
                else
                {
                    return base.SequenceFunc ( StatusSequence.eFinish, data );
                }
            }
            return StatusSequence.eContinue;
        }
    }
}
