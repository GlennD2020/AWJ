/**
 * @file:   MathLibrary.cs
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nsAlexKir
{
    namespace MathLibrary
    {
        /// <summary> Класс с функциями расчета контольных сумм. </summary>
        public class CRC
        {
            /// <summary> Функция расчета CRC16. Данный алгоритм используется в протоколе Modbus. </summary>
            /// <remarks> Используемы полином 0xA001. </remarks>
            /// <param name="buf"> Входной Буфер. </param>
            /// <param name="len"> Размер буфера. </param>
            /// <returns> Расчитанное значение. </returns>
            public static UInt16 CalculateCRC16 ( byte[] buf, int len )
            {
                UInt16 crc = 0xFFFF;
                for (int pos = 0; pos < len; pos++)
                {
                    crc ^= (UInt16)buf[pos];
                    for (int i = 8; i != 0; i--)
                    {
                        if ((crc & 0x0001) != 0)
                        {
                            crc >>= 1;
                            crc ^= 0xA001;
                        }
                        else
                        {
                            crc >>= 1;
                        }
                    }
                }
                return crc;
            }
        }
    }
}
