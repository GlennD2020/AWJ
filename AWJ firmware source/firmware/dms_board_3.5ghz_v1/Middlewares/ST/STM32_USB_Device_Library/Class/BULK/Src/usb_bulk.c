/********************************************************************************
 *	@file:		usb_bulk.h														*
 *	@remark:	Library to work USB library.									*
 *	@author:	Kirillov A.V.													*
 ********************************************************************************/
#define __C_USB_BULK_

/********************************************************************************
 *						INCLUDES												*
 ********************************************************************************/
#include "usb_bulk.h"
#include "usb_bulk_if.h"
#include "usbd_desc.h"
#include "usbd_ctlreq.h"
#include "dms_functions.h"
 
/********************************************************************************
 *					DECLARATION STATIC FUNCTIONS 								 *
 *********************************************************************************/
static uint8_t  USBD_BULK_Init					 ( USBD_HandleTypeDef *pdev, uint8_t cfgidx );
static uint8_t  USBD_BULK_DeInit				 ( USBD_HandleTypeDef *pdev, uint8_t cfgidx );
static uint8_t  USBD_BULK_Setup					 ( USBD_HandleTypeDef *pdev, USBD_SetupReqTypedef *req );
static uint8_t*	USBD_BULK_GetCfgDesc			 ( uint16_t *length );
static uint8_t*	USBD_BULK_GetDeviceQualifierDesc ( uint16_t *length );
static uint8_t  USBD_BULK_DataIn				 ( USBD_HandleTypeDef *pdev, uint8_t epnum );
static uint8_t  USBD_BULK_DataOut				 ( USBD_HandleTypeDef *pdev, uint8_t epnum );
 
/********************************************************************************
 *					DECLARATION STRUCTS											*
 ********************************************************************************/
/** 
 *	@brief Настройка функций обратного вызова.
 */ 
USBD_ClassTypeDef  USBD_BULK_ClassDriver = 
{
	//	Функции, относящиеся к классу.
	USBD_BULK_Init,
	USBD_BULK_DeInit,
	//	Функции, относящиеся к контрольной точке
	USBD_BULK_Setup,
	NULL,  
	NULL,
	//	Функции, относящиеся к классу. Конечные точки отличные от контрольной.
	USBD_BULK_DataIn,
	USBD_BULK_DataOut,
	NULL,
	NULL,
	NULL, 
	//	Функции возвращяющие дескрипторы.
	//	Дескриптор конфигурации: HS
	USBD_BULK_GetCfgDesc,
	//	Дескритор конфигурации: FS
	USBD_BULK_GetCfgDesc, 
	//	Дескриптор конфигурации: другие скорости
	USBD_BULK_GetCfgDesc,
	//	Уточняющий дескриптор конфигурации.
	USBD_BULK_GetDeviceQualifierDesc,
};

/**
 * @brief Дескриптор конфигурации устройства.						
 */
static uint8_t USBD_BULK_CfgDesc[USB_BULK_CONFIG_DESC_SIZE] =
{
	// 	КОНФИГУРАТОР ДЕСКРИПТОРА УСТРОЙСТВА. Размер: 9 байт.
	USB_LEN_CFG_DESC, 					//	Размер дескриптора в байтах, стандартная длина 9 байт.
	USB_DESC_TYPE_CONFIGURATION, 		//	Тип дескриптора: Configuration
	//	Общий объем данных возвращаемых для данной кофигурации, 2 байта
	//	Включает сумму длин всех дескрипторов: конфигурации, интерфейса, 
	//	контрольных точек и специальных дескрипторов (класса или производителя).
	USB_BULK_CONFIG_DESC_SIZE,			//	wTotalLength: Bytes returned
	0x00,
	0x01,         						//	Количество интерфейсов, поддерживаемых данной конфигурацией.
	0x01,         						//	Идентификатор текущей конфигурации.
	USBD_IDX_CONFIG_STR,				//	Индекс строкового дескриптора, описывающего данную конфигурацию.
	0xC0,         						//	Битовая маска: bus powered and Supports Remote Wakeup
	0xFA,         						//	MaxPower 500 mA: this current is used for detecting Vbus
	
	// 	ОПИСАНИЕ ДЕСКРИПТОРА ИНТЕРФЕЙСА. Размер: 9 байт.
	USB_LEN_IF_DESC,					//	Размер дескриптора в байтах, стандартная длина 9 байт.
	USB_DESC_TYPE_INTERFACE,			//	Тип дескриптора: Interface descriptor type
	0x00,         						//  Номер данного интерфейса, нумерация начинается с нуля.
	0x00,         						//	Альтернативный номер интерфейса.
	0x02,         						//	Число используемых конечных точек. Будем использовать 2 конечные точки, без учета нулевой.
	//	Непосредственно идентификатор класса, подкласса и типа протокола устройства.
	0x00,         						//	bInterfaceClass: 	-
	0x00,         						//	bInterfaceSubClass: -
	0x00,         						//	nInterfaceProtocol:	-
	USBD_IDX_INTERFACE_STR,				//	Индекс строкового дескриптора, описывающий данный интерфейс.
	
	//	ДЕСКРИПТОР КОНЕЧНОЙ ТОЧКИ. КОНЕЧНАЯ ТОЧКА IN. Размер: 7 байт.
	USB_LEN_EP_DESC,					//	Размер дескриптора конечной точки.
	USB_DESC_TYPE_ENDPOINT,				//	Тип дескриптора.
	USB_BULK_EPIN_ADDR,					//	Адрес конечной точки.
	USBD_EP_TYPE_BULK,        			//	Атрибуты конечной точки: BULK.
	USB_BULK_EPIN_SIZE,					//	wMaxPacketSize: 2 Byte max. Максимальный размер пакета конечной точки, 2 байта.
	0x00,
	0x00,          						//	Интервал опроса конечной точки. Данный параметр действителен только для типа Interrupt.

	//	ДЕСКРИПТОР КОНЕЧНОЙ ТОЧКИ. КОНЕЧНАЯ ТОЧКА OUT. Размер: 7 байт.
	USB_LEN_EP_DESC,					//	Размер дескриптора конечной точки.
	USB_DESC_TYPE_ENDPOINT,				//	Тип дескриптора.
	USB_BULK_EPOUT_ADDR,				//	Адрес конечной точки.
	USBD_EP_TYPE_BULK,					//	Атрибуты конечной точки: BULK
	USB_BULK_EPOUT_SIZE,				//	Максимальный размер пакета конечной точки, 2 байта.
	0x00,
	0x00,          						//	Интервал опроса конечной точки. Данный параметр действителен только для типа Interrupt.
};

/**
 * @brief 	Уточняющий дескриптор устройства.
 * @remark	Используется при работе устройства HS на других скоростях.
 */
static uint8_t USBD_BULK_DeviceQualifierDesc[USB_LEN_DEV_QUALIFIER_DESC] =
{
	USB_LEN_DEV_QUALIFIER_DESC,			//	Размер уточняющего дескриптора.	
	USB_DESC_TYPE_DEVICE_QUALIFIER,		//	Тип дескриптора.
	//	Версия протокола USB, 2 байта.
	0x00,								//  bcdUSB
	0x02,								//  bcdUSB
	//	Идентификатор класса, подкласса, протокола.
	0x00,								//  bDeviceClass
	0x00,								//  bDeviceSubClass
	0x00,								//  bDeviceProtocol
	0x40,								//	Максимальный размер пакета для нулевой конечной точки для других скоростей работы.	
	0x01,								//	Количество дополнительных конфигураций устройства.
	0x00,								//	Зарезервировано, должно быть равно нулю.
};

/********************************************************************************
 *						FUNCTION												*
 ********************************************************************************/
/**
 * @brief	USBD_BULK_Init
 *			Initialize the USB interface
 * @param	pdev: device instance
 * @param	cfgidx: Configuration index
 * @retval	status
 */
static uint8_t  USBD_BULK_Init			( USBD_HandleTypeDef *pdev, uint8_t cfgidx )
{
	uint8_t status=USBD_OK;

	//	Задача инициализации запустить конечные точки, описанные в дескрипторах конечных точек для данного интерфейса.
	USBD_LL_OpenEP ( pdev, USB_BULK_EPOUT_ADDR, USBD_EP_TYPE_BULK, USB_BULK_EPOUT_SIZE );
	USBD_LL_OpenEP ( pdev, USB_BULK_EPIN_ADDR,  USBD_EP_TYPE_BULK, USB_BULK_EPIN_SIZE  );

	//	Создаем пользовательскую структуру данных для работы с классом.
	pdev->pClassData = (USBD_BULK_HandleTypeDef*)USBD_malloc(sizeof(USBD_BULK_HandleTypeDef));

	//	Инициализируем буфер данных класса.
	((USBD_BULK_HandleTypeDef*)pdev->pClassData)->SizeTxBuffer = USB_BULK_BUFFER_IN_SIZE;
	((USBD_BULK_HandleTypeDef*)pdev->pClassData)->SizeRxBuffer = USB_BULK_BUFFER_OUT_SIZE;

	//	Готовим конечную точку к приему новых данных.
	USBD_LL_PrepareReceive ( pdev, USB_BULK_EPOUT_ADDR, ((USBD_BULK_HandleTypeDef*)pdev->pClassData)->RxBuffer, USB_BULK_EPOUT_SIZE );	

	return status;
}

/**
 * @brief	USBD_BULK_DeInit
 *			DeInitialize the USB interface
 * @param	pdev: device instance
 * @param	cfgidx: Configuration index
 * @retval	status
 */
static uint8_t  USBD_BULK_DeInit		( USBD_HandleTypeDef* pdev, uint8_t cfgidx )
{
	uint8_t status = USBD_OK;

	//	Закрытие конечных точек устройства.
	USBD_LL_CloseEP ( pdev, USB_BULK_EPOUT_ADDR );
	USBD_LL_CloseEP ( pdev, USB_BULK_EPIN_ADDR  );

	//	Удаляем пользовательскую структуру.
	if ( pdev->pClassData!=NULL )
	{
		USBD_free ( pdev->pClassData );
		pdev->pClassData = NULL;
	}	
	
	return status;
}

/**
 * @brief  USBD_CDC_RegisterInterface
 * @param  pdev: Device instance
 * @param  fops: Interface callback
 * @retval status
 */
uint8_t  USBD_BULK_RegisterInterface 	( USBD_HandleTypeDef* pdev, tsUSB_BULK_IF *fops )
{
	uint8_t result = USBD_FAIL;
	
	if ( fops!=NULL )
	{
		pdev->pUserData = fops;
		result = USBD_OK;
	}
	
	return result;
}

/**
 *	@brief	USBD_BULK_Setup
 *	@param	pdev: instance
 *	@param	req: USB request
 *	@retval	Result of the function
 *
*	@remark	Функция обработки запросов через контрольную точку.
 */
static uint8_t	USBD_BULK_Setup			( USBD_HandleTypeDef *pdev, USBD_SetupReqTypedef *req )
{
	uint8_t status = USBD_OK;
	uint8_t buf[16];
	//USBD_BULK_HandleTypeDef* pBulk = (USBD_BULK_HandleTypeDef*)pdev->pClassData;	

	switch ( req->bmRequest&USB_REQ_TYPE_MASK )
	{
		case USB_REQ_TYPE_CLASS:
			switch ( req->bRequest )
			{
				case 1:	// Управление состоянием светодиода.
					DeviceCfg.Led.Ctrl 	   = req->wIndex;
					DeviceCfg.Led.Interval = req->wValue;
					USBD_CtlSendStatus ( pdev ); //	Необходимо отправить для подтверждения обработки команды.
					break;

				case 2: // Тестовое считывание информации.
					for ( int i=0; i<8; i++ )
					{
						buf[i]=8-i-1;
					}
					USBD_CtlSendData ( pdev, buf, 8 );
					break;
				
				default:
					break;
			}
			break;

		default:
			break;
	}

	return status;
}

/**
 *	@brief	USBD_BULK_GetCfgDesc
 *	@param	length:	Size of array.
 *	@retval	Return pointer to array of USB configuration.
 */
static uint8_t*	USBD_BULK_GetCfgDesc	( uint16_t *length )
{
	*length = sizeof (USBD_BULK_CfgDesc);
	return USBD_BULK_CfgDesc;
}

/**
 *	@brief	USBD_BULK_GetDeviceQualifierDesc
 *	@param	length:	Size of array.
 *	@retval	Return pointer to array of USBD_BULK_DeviceQualifierDesc.
 */
static uint8_t*	USBD_BULK_GetDeviceQualifierDesc ( uint16_t *length )
{
	*length = sizeof (USBD_BULK_DeviceQualifierDesc);
	return USBD_BULK_DeviceQualifierDesc;
}

/**
 *	@brief  USBD_BULK_DataOut
 *		    Data sent on non-control IN endpoint
 *	@param  pdev:	device instance
 *	@param  epnum:	endpoint index
 *	@retval status
 *
 *	@remark 
 */
static uint8_t  USBD_BULK_DataIn		( USBD_HandleTypeDef *pdev, uint8_t epnum )
{
	USBD_BULK_HandleTypeDef* pBulk = (USBD_BULK_HandleTypeDef*)pdev->pClassData;

	if ( pBulk!=NULL )
	{
		return USBD_OK;
	}
	else
	{
		return USBD_FAIL;
	}
}

/**
 * @brief  	USBD_BULK_DataOut
 *         	Data received on non-control Out endpoint
 * @param 	pdev:	device instance
 * @param	epnum:	endpoint index
 * @retval	status
 *
 * @remark	Функция приема
 */
static uint8_t  USBD_BULK_DataOut		( USBD_HandleTypeDef *pdev, uint8_t epnum )
{
	uint8_t status = USBD_OK;
	USBD_BULK_HandleTypeDef* pBulk = (USBD_BULK_HandleTypeDef*)pdev->pClassData;
	tsUSB_BULK_IF* pHL_Func = (tsUSB_BULK_IF*)pdev->pUserData;

	if ( pBulk!=NULL )
	{
		pBulk->LengthRxData = USBD_LL_GetRxDataSize ( pdev, epnum );

		if ( pHL_Func!=NULL )
		{
			//	Прием данных
			pHL_Func->Receive ( pBulk->RxBuffer, &pBulk->LengthRxData );
		}

		//	Готовим конечную точку к приему новых данных.
		USBD_LL_PrepareReceive ( pdev, USB_BULK_EPOUT_ADDR, pBulk->RxBuffer, USB_BULK_EPOUT_SIZE );

		status = USBD_OK;
	}
	else
	{
		status = USBD_FAIL;
	}

	return status;
}

/**
 * @brief	USBD_BULK_TransmitData
 * @param	pdev: instance
 * @param	Buf
 * @param	Len
 * @retval	status
 */
uint8_t USBD_BULK_TransmitData 			( USBD_HandleTypeDef *pdev, uint8_t* Buf, uint16_t Len )
{
	uint8_t result = USBD_OK;

    /* Update the packet total length */
    pdev->ep_in[USB_BULK_EPIN_ADDR & 0xFU].total_length = Len;

    USBD_LL_Transmit ( pdev, USB_BULK_EPIN_ADDR, Buf, Len );

	return result;
}

/********************* END OF FILE *********************************************/
