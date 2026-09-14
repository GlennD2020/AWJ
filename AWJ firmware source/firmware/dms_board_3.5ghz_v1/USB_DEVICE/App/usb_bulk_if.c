
/********************************************************************************
 *						INCLUDES												*
 ********************************************************************************/
#include "usb_bulk_if.h"

/********************************************************************************
 *					DECLARATION FUNCTIONS										*
 ********************************************************************************/
static uint8_t Bulk_Init	( void );
static uint8_t Bulk_DeInit  ( void );
static uint8_t Bulk_Control ( uint8_t cmd, uint8_t *pbuf, uint16_t length );
static uint8_t Bulk_Receive ( uint8_t *Buf, uint16_t *Len );

/********************************************************************************
 *					EXTERN FUNCTIONS										*
 ********************************************************************************/
extern uint8_t dms_recv_bulk_data ( uint8_t *Buf, uint16_t *Len );

/********************************************************************************
 *						VARIABLES												*
 ********************************************************************************/
tsUSB_BULK_IF UsbBulk_IF = 
{
	Bulk_Init,
	Bulk_DeInit,
	Bulk_Control,
	Bulk_Receive,
	Bulk_Transmite
};
 
/********************************************************************************
 *						FUNCTIONS												*
 ********************************************************************************/
/**
 *	@brief	Bulk_Init
 *	@retval	Result
 */
static uint8_t Bulk_Init ( void )
{
	uint8_t result = USBD_OK;
	return result;
}

/**
 *	@brief	Bulk_DeInit
 *	@retval	Result
 */
static uint8_t Bulk_DeInit ( void )
{
	uint8_t result = USBD_OK;
	return result;
}

/**
 *	@brief	Bulk_Control
 *	@param	cmd:
 *	@param	pbuf:
 *	@param	length:
 *	@retval	Result
 */
static uint8_t Bulk_Control ( uint8_t cmd, uint8_t *pbuf, uint16_t length )
{
	uint8_t result = USBD_OK;
	return result;
}

/**
 *	@brief	Bulk_Receive
 *	@remark	Функция обработки принятых данных.
 *	@param	Buf: Указатель на принятые данные
 *	@param	Len: Кол-во принятых данных.
 *	@retval	Result.
 */
static uint8_t Bulk_Receive ( uint8_t *Buf, uint16_t *Len )
{
	uint8_t result = USBD_OK;
	if ( *Len>0 ) {
		dms_recv_bulk_data ( Buf, Len );
	}
	return result;
}

/**
 *	@brief	Bulk_Transmite.
 *	@param	Buf: 
 *	@param	Len: 
 *	@retval	Result.
 */
uint8_t Bulk_Transmite ( uint8_t *Buf, uint16_t *Len )
{
	uint8_t result = USBD_OK;
	return result;
}
