/**
 * File:    SystemCommands.cs
 * Author:  Kirillov A.V.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace nsAlexKir
{
    /// <summary> Тип команды. </summary>
    public enum eTYPE_COMMANDS : short
    {
        TEST_CONNECT = 1,       //  Тест соединения.
        GET_VERSIONS,           //  Команда вычитывания версия программ.
        GET_STATE,              //  Чтение состояния устройства.
        READ_MEMORY,            //  Чтение памяти.
        WRITE_MEMORY,           //  Запись памяти.
        GET_PLL_REG,            //  Считывание регистра ФАПЧ.
        SET_PLL_REG,            //  Запись регистра ФАПЧ.
        GET_DAC_VALUE,          //  Считывание значения ЦАПа.
        SET_DAC_VALUE,          //  Установка значения ЦАПа.
        GET_SWEEP_PLL,          //  Свип по частоте.
        SET_SWEEP_PLL,          //  Свип по частоте.
        GET_PULSE_CTRL,         //  Управление модулятором.
        SET_PULSE_CTRL,         //  Управление модулятором.
        GET_AMPL_MOD_CTRL,      //  Управление амплитудной модуляции.
        SET_AMPL_MOD_CTRL,      //  Управление амплитудной модуляции.
        GET_CTRL_POWER_DET,     //  Считывание границы.
        SET_CTRL_POWER_DET,     //  Задание границы.
        GET_DIP_FREQ,           //  Считывание частоты переключателя.
        SET_DIP_FREQ,           //  Установка частоты переключателя.
        GET_TIME_INTERVAL,      //  Считвание длительности звуковых сигналов.
        SET_TIME_INTERVAL,      //  Установка длительности звуковых сигналов.
        GET_DDS_REG,            //  Считывание регистра DDS.
        SET_DDS_REG,            //  Запись регистра DDS.
        GET_DDS_CTR_FREQ,       //  Считывание частоты управления DDS.
        SET_DDS_CTR_FREQ,       //  Установка частоты управления DDS.
        GET_SWEEP_DDS,          //	Считывание настроек росчерка DDS.
        SET_SWEEP_DDS,          //	Установка настроек росчерка DDS.
        GET_RAMP_DDS,           //	Считывание настроек аппаратного росчерка DDS.
        SET_RAMP_DDS,           //	Установка настроект аппаратного росчерка DDS.
        RESET_DDS,              //	Сброс микросхемы DDS.
        DEFAULT,                //  Команда перевода устройство в состояние по умолчанию.
        WRITE_AD9106_REGISTER,	//  Запись регистра AD9106 для Pattern Memory.
    }

    /// <summary> Статус выполнения команды. </summary>
    public enum eSTATUS_COMMANDS : short
    {
        NO_ERROR = 0,			//  0 -	Нет ошибки.
        DONE, 			        //  1 - Команда выполнена.
        UNDEFINED,			    //  2 - Команда не определена.
        DEVICE_ERROR,		    //  3 -	Ошибка устройства.
        RECV_CMD_ERROR,		    //  4 -	Ошибка в принятой команде.
        EEPROM_BUSY,		    //  5 -	Микросхема памяти занята.
        WRITE_EEPROM_ERROR,	    //  6 -	Ошибка записи данных в память.
        READ_EEPROM_ERROR,	    //  7 -	Ошибка чтения данных из памяти.
    }

    /// <summary> Status of the command. </summary>
    public static class StatusCommand
    {
        public static String[] text =
        {
            "No error",
            "Command is done",
            "Undefined command",
            "Device error",
            "Error received command",
            "Memory is busy",
            "Write eeprom error",
            "Read eeprom error",
        };
    }

    /// <summary> Структура ответа. </summary>
    public class Command : ICloneable
    {
        /// <summary> Функция получения размера команды. </summary>
        /// <returns> Размер команды. </returns>
        public const int CommandSize = 32;

        /// <summary> Размер заголовка. </summary>
        public const int HEAD_SIZE = 6;

        /// <summary> Получение размера команды. </summary>
        /// <returns> Размер команды. </returns>
        public static int GetCommandSize() { return CommandSize; }

        /// <summary> Тело команды. </summary>
        public byte[] body = new byte[CommandSize];

        /// <summary> Конструктор класса. </summary>
        public Command()
        {
            Type = eTYPE_COMMANDS.TEST_CONNECT;
            Status = eSTATUS_COMMANDS.NO_ERROR;
            LenData = 0;
        }

        /// <summary> Конструктор класса. </summary>
        public Command(eTYPE_COMMANDS cmd)
        {
            Status = eSTATUS_COMMANDS.NO_ERROR;
            Type = cmd;
            LenData = 0;
        }

        /// <summary> Конструктор класса. </summary>
        public Command(byte[] data)
        {
            if (data.Length == CommandSize)
            {
                for (int i = 0; i < CommandSize; i++)
                {
                    body[i] = data[i];
                }
            }
        }

        /// <summary> Конструктор класса </summary>
        /// <param name="data"> массив данных </param>
        /// <param name="size"> кол-во данных </param>
        public Command(byte[] data, int size)
        {
            for (int i = 0; i < size; i++)
            {
                body[i] = data[i];
            }
        }

        /// <summary> Функция клонирования объекта. </summary>
        /// <returns> Клон объекта </returns>
        public object Clone()
        {
            return this.MemberwiseClone();
        }

        /// <summary> Тип команды. </summary>
        public eTYPE_COMMANDS Type
        {
            get
            {
                return (eTYPE_COMMANDS)((int)body[1] * 256 + (int)body[0]);
            }
            set
            {
                body[0] = (byte)(((int)value >> 0) & 0xFF);
                body[1] = (byte)(((int)value >> 8) & 0xFF);
            }
        }

        /// <summary> Статус команды. </summary>
        public eSTATUS_COMMANDS Status
        {
            get
            {
                return (eSTATUS_COMMANDS)(body[2] + (short)body[3] * 256);
            }
            set
            {
                body[2] = (byte)((short)value & 0xFF);
                body[3] = (byte)(((short)value >> 8) & 0xFF);
            }
        }

        /// <summary> Длина данных. </summary>
        public short LenData
        {
            get
            {
                return (short)(body[4] + body[5] * 256);
            }
            set
            {
                body[4] = (byte)(value & 0xFF);
                body[5] = (byte)((value >> 8) & 0xFF);
            }
        }

        #region GET/SET BYTE
        /// <summary> Установка байта. </summary>
        /// <param name="value"> Значение байта </param>
        /// <param name="offset"> Смещение. </param>
        public void setByte(Byte value, int offset)
        {
            body[HEAD_SIZE + offset] = value;
        }

        /// <summary> Считывание байта. </summary>
        /// <param name="offset"> Смещение </param>
        /// <returns> Байт дынных </returns>
        public byte getByte(int offset)
        {
            return body[HEAD_SIZE + offset];
        }
        #endregion

        #region GET/SET SHORT
        /// <summary></summary>
        /// <param name="value"></param>
        /// <param name="offset"></param>
        public void setShort(Int16 value, int offset)
        {
            body[HEAD_SIZE + offset + 0] = (byte)((value >> 0) & 0xFF);
            body[HEAD_SIZE + offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        /// <summary> Считывание значения параметра типа UShort. </summary>
        /// <param name="offset"> Смещение параметра. Внимание! Смещение задается в для байтового массива. </param>
        /// <returns> Считанный параметр. </returns>
        public short geUShort(int offset)
        {
            short value = 0;
            value = (short)((body[HEAD_SIZE + offset + 0]) * (1 << 0));
            value += (short)((body[HEAD_SIZE + offset + 1]) * (1 << 8));
            return value;
        }
        #endregion

        #region GET/SET USHORT
        /// <summary></summary>
        /// <param name="value"></param>
        /// <param name="offset"></param>
        public void setUShort(ushort value, int offset)
        {
            body[HEAD_SIZE + offset + 0] = (byte)((value >> 0) & 0xFF);
            body[HEAD_SIZE + offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        /// <summary> Считывание значения параметра типа UShort. </summary>
        /// <param name="offset"> Смещение параметра. Внимание! Смещение задается в для байтового массива. </param>
        /// <returns> Считанный параметр. </returns>
        public ushort getUShort(int offset)
        {
            ushort value = 0;
            value = (ushort)((body[HEAD_SIZE + offset + 0]) * (1 << 0));
            value += (ushort)((body[HEAD_SIZE + offset + 1]) * (1 << 8));
            return value;
        }
        #endregion

        #region GET/SET ULONG
        /// <summary> Установка числа формата INT </summary>
        /// <param name="value"> Число. </param>
        /// <param name="index"> Индекс массива. </param>
        public void setULong(ulong value, int offset)
        {
            byte[] data = BitConverter.GetBytes(value);
            body[HEAD_SIZE + offset + 0] = data[0];
            body[HEAD_SIZE + offset + 1] = data[1];
            body[HEAD_SIZE + offset + 2] = data[2];
            body[HEAD_SIZE + offset + 3] = data[3];
        }

        /// <summary> Считывание параметра </summary>
        /// <param name="index"> Индекс переменной. Смещение задается в для байтового массива. </param>
        /// <returns> Считанная переменная. </returns>
        public ulong getULong(int offset)
        {
            ulong value = 0;
            value += ((ulong)(body[HEAD_SIZE + offset + 0])) * (1 << 0);
            value += ((ulong)(body[HEAD_SIZE + offset + 1])) * (1 << 8);
            value += ((ulong)(body[HEAD_SIZE + offset + 2])) * (1 << 16);
            value += ((ulong)(body[HEAD_SIZE + offset + 3])) * (1 << 24);
            return value;
        }
        #endregion

        #region GET/SET INT
        /// <summary> Установка числа формата INT </summary>
        /// <param name="value"> Число. </param>
        /// <param name="index"> Индекс массива. </param>
        public void setInt(int value, int offset)
        {
            byte[] data = BitConverter.GetBytes(value);
            body[HEAD_SIZE + offset + 0] = data[0];
            body[HEAD_SIZE + offset + 1] = data[1];
            body[HEAD_SIZE + offset + 2] = data[2];
            body[HEAD_SIZE + offset + 3] = data[3];
        }

        /// <summary> Считывание параметра </summary>
        /// <param name="index"> Индекс переменной. Смещение задается в для байтового массива. </param>
        /// <returns> Считанная переменная. </returns>
        public int getInt(int offset)
        {
            int value = 0;
            value += ((int)(body[HEAD_SIZE + offset + 0])) * (1 << 0);
            value += ((int)(body[HEAD_SIZE + offset + 1])) * (1 << 8);
            value += ((int)(body[HEAD_SIZE + offset + 2])) * (1 << 16);
            value += ((int)(body[HEAD_SIZE + offset + 3])) * (1 << 24);
            return value;
        }
        #endregion

        #region GET/SET UINT
        /// <summary> Установка числа формата INT </summary>
        /// <param name="value"> Число. </param>
        /// <param name="index"> Индекс массива. </param>
        public void setInt(uint value, int offset)
        {
            byte[] data = BitConverter.GetBytes(value);
            body[HEAD_SIZE + offset + 0] = data[0];
            body[HEAD_SIZE + offset + 1] = data[1];
            body[HEAD_SIZE + offset + 2] = data[2];
            body[HEAD_SIZE + offset + 3] = data[3];
        }

        /// <summary> Считывание параметра </summary>
        /// <param name="index"> Индекс переменной. Смещение задается в для байтового массива. </param>
        /// <returns> Считанная переменная. </returns>
        public uint getUInt(int offset)
        {
            uint value = 0;
            value += ((uint)(body[HEAD_SIZE + offset + 0])) * (1 << 0);
            value += ((uint)(body[HEAD_SIZE + offset + 1])) * (1 << 8);
            value += ((uint)(body[HEAD_SIZE + offset + 2])) * (1 << 16);
            value += ((uint)(body[HEAD_SIZE + offset + 3])) * (1 << 24);
            return value;
        }
        #endregion

        #region GET/SET FLOAT
        /// <summary> Установка значения плавающей переменной </summary>
        /// <param name="value"> Переменная для записи. </param>
        /// <param name="offset"> Индекс смещения для записи переменной. </param>
        public void setFloat(float value, int offset)
        {
            byte[] data = BitConverter.GetBytes(value);
            body[HEAD_SIZE + offset + 0] = data[0];
            body[HEAD_SIZE + offset + 1] = data[1];
            body[HEAD_SIZE + offset + 2] = data[2];
            body[HEAD_SIZE + offset + 3] = data[3];
        }

        /// <summary> Считывание параметра </summary>
        /// <param name="index"> Индекс переменной. Смещение задается в для байтового массива. </param>
        /// <returns> Считанная переменная. </returns>
        public float getFloat(int offset)
        {
            float value = BitConverter.ToSingle(body, HEAD_SIZE + offset);
            return value;
        }
        #endregion
    }
}