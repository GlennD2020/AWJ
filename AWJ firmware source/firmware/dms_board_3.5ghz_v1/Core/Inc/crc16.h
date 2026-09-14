/**
 ********************************************************
 *	@file:			crc16.h
 *	@version:		v1.0
 ********************************************************
 */
#ifndef __H_CRC16_
#define __H_CRC16_

#ifdef __cplusplus
extern "C" {
#endif

unsigned short CalculateCRC16	( unsigned char* buf, int len );
	
#ifdef __cplusplus
}
#endif
	
#endif	//	__H_CRC16_
