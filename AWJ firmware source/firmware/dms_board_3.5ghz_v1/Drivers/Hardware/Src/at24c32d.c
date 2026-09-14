/*******************************************************************************
 *	@file:		at24c32d.c
 *	@brief:		I2C EEPROM driver for AT24C32D / AT24C64.
 *	@note:		K9 FIX: Added page-boundary-safe writes + write cycle delay.
 *******************************************************************************/
#define __C_I2C_MEM_AT24_

/*******************************************************************************
 *		INCLUDES
 *******************************************************************************/
#include "at24c32d.h"

/** AT24C32 page size = 32 bytes. AT24C64 also = 32 bytes. */
#define AT24_PAGE_SIZE  32

/*******************************************************************************
 *		FUNCTIONS
 *******************************************************************************/
/**
 *	@brief		AT24_I2C_SetPeriph
 *	@remark		Configure I2C peripheral for EEPROM.
 *	@param		hi2c - Pointer to I2C handle.
 *	@retval		0
 */
int AT24_I2C_SetPeriph		( I2C_HandleTypeDef* hi2c )
{
	mem_periph.hi2c = hi2c;
	return 0;
}

/**
 *	@brief		AT24_I2C_MemoryRead.
 *	@remark		Read data from AT24C32D EEPROM.
 *	@param		address - EEPROM address to read from.
 *	@param		data    - Buffer for read data.
 *	@param		num     - Number of bytes to read.
 *	@retval		1 = success, -1 = error, -2 = busy
 */
int AT24_I2C_MemoryRead 	( int address, unsigned char* data, uint16_t num )
{
	HAL_StatusTypeDef status = HAL_OK;

	status = HAL_I2C_Mem_Read ( mem_periph.hi2c, (AT24C64_ADDRESS<<1), address, I2C_MEMADD_SIZE_16BIT, data, num, 2000 );

	if ( status == HAL_BUSY )	return -2;
	else if ( status!=HAL_OK )	return -1;
	else return 1;
}

/**
 *	@brief		AT24_I2C_MemoryWrite
 *	@remark		Write data to EEPROM with page-boundary handling.
 *				AT24C32/C64 have 32-byte pages. A single I2C write MUST NOT
 *				cross a page boundary or the address wraps within the page,
 *				corrupting data. This function splits writes at page boundaries
 *				and waits for the write cycle (5ms max) between pages.
 *
 *	@param		address - EEPROM address to write to.
 *	@param		data    - Pointer to data buffer.
 *	@param		num     - Number of bytes to write.
 *	@retval		1 = success, -1 = error, -2 = busy
 */
int AT24_I2C_MemoryWrite 	( int address, unsigned char* data, uint16_t num )
{
	HAL_StatusTypeDef status;

	while ( num > 0 )
	{
		/* Calculate how many bytes remain in the current page */
		uint16_t page_offset   = address % AT24_PAGE_SIZE;
		uint16_t bytes_in_page = AT24_PAGE_SIZE - page_offset;
		uint16_t chunk         = ( num < bytes_in_page ) ? num : bytes_in_page;

		status = HAL_I2C_Mem_Write ( mem_periph.hi2c, (AT24C64_ADDRESS<<1),
		                             address, I2C_MEMADD_SIZE_16BIT,
		                             data, chunk, 2000 );

		if ( status == HAL_BUSY )   return -2;
		if ( status != HAL_OK )     return -1;

		/* AT24 write cycle: max 5ms. Must wait before next write. */
		HAL_Delay(5);

		address += chunk;
		data    += chunk;
		num     -= chunk;
	}

	return 1;
}
