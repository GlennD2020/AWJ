/**
 ********************************************************
 *	@file:			crc16.c
 *	@version:		v1.0
 ********************************************************
 */
#include "crc16.h"

/**
 *	@brief 	CalculateCRC16
 *	@remark	Функция расчета CRC16. Данный алгоритм используется в протоколе Modbus.
 *	@param	buf - Входной буфер.
 *	@param	len - Кол-во принятых данных.
 *	@return Результат расчета CRC.
 */
unsigned short CalculateCRC16	( unsigned char* buf, int len )
{
	unsigned short crc = 0xFFFF;
	for ( int pos=0; pos<len; pos++ )
	{
		crc^=(unsigned short)buf[pos];
		for ( int i=8; i!=0; i-- )
		{
			if ( (crc & 0x0001)!= 0 )
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
