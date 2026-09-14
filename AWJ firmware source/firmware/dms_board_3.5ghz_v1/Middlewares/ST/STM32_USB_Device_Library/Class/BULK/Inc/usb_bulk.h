/********************************************************************************
 *	@file:		usb_bulk.h														*
 *	@remark:	Library to work USB library.									*
 *	@author:	Kirillov A.V.													*
 ********************************************************************************/
#ifndef __H_USB_BULK_
#define __H_USB_BULK_

/********************************************************************************
 *						INCLUDES												*
 ********************************************************************************/
#include  "usbd_ioreq.h"

/********************************************************************************
 *					DEFINES										 				*
 ********************************************************************************/
//	Конечная точка для отправки данных
#define USB_BULK_EPIN_ADDR				0x81
#define USB_BULK_EPIN_SIZE				0x40 //	64 байта
   
//	Конечная точка для принятия данных
#define USB_BULK_EPOUT_ADDR				0x01
#define USB_BULK_EPOUT_SIZE				0x40 //	64 байта

//	Суммарная длина дескриптора конфигурации, включая все вложенные дескрипторы.
//	Вычисляется, после заполнения дескриптора конфигурации
#define USB_BULK_CONFIG_DESC_SIZE      	32

#define USB_BULK_BUFFER_IN_SIZE			256
#define USB_BULK_BUFFER_OUT_SIZE		256

/********************************************************************************
 *					DECLARATION STRUCTS							 				*
 ********************************************************************************/
/**
 *	@brief Типовая структура интерфейсных функций.
 */
typedef struct
{
	uint8_t (*Init)(void);
	uint8_t (*DeInit)(void);
	uint8_t (*Control)(uint8_t cmd, uint8_t *pbuf, uint16_t length);
	uint8_t (*Receive)(uint8_t *Buf, uint16_t *Len);
	uint8_t (*Transmite)(uint8_t *Buf, uint16_t *Len);	
} tsUSB_BULK_IF;

/**
 *	@brief Типовая структура для работы с данными...
 */
typedef struct
{	//	Буфер для отправки данных.
	uint8_t   TxBuffer[USB_BULK_BUFFER_IN_SIZE];
	//	Размер буфера отправляемых данных.
	uint16_t  SizeTxBuffer;							
	//	Количество отправляемых данных.
	uint16_t  LengthTxData;							
	
	//	Буфер для приема данных.
	uint8_t   RxBuffer[USB_BULK_BUFFER_OUT_SIZE];
	//	Размер буфера для приема данных.
	uint16_t  SizeRxBuffer;
	//	Количество принятых данных.
	uint16_t  LengthRxData;
} USBD_BULK_HandleTypeDef;

#ifndef __C_USB_BULK_
	extern USBD_ClassTypeDef  USBD_BULK_ClassDriver;
#endif

/********************************************************************************
 *					EXPORTED STRUCTS							 				*
 ********************************************************************************/

uint8_t  USBD_BULK_RegisterInterface ( USBD_HandleTypeDef* pdev, tsUSB_BULK_IF *fops );
uint8_t	 USBD_BULK_TransmitData 	 ( USBD_HandleTypeDef *pdev, uint8_t* Buf, uint16_t Len );

#endif	///	__H_USB_BULK_

/*************************** END OF FILE ****************************************/
