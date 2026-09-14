/***********************************************************************
 *	@file:		ad5932.c
 *	@brief:		Испольнительный файл библиотеки для работы с AD5932.
 *	@version:	v1.0
 *  
 *	@author:	Kirillov A.V.
 *	@date:		15/12/2019
 ***********************************************************************/
 #define __C_AD5932_
/***********************************************************************
 *			INCLUDES
 ***********************************************************************/
 #include "ad5932.h"

/***********************************************************************
 *			FUNCTIONS
 ***********************************************************************/
/**
 *	@brief		Массив регистров для микросхемы AD5932.
 *	@remark		Массив для инициализации микросхемы.
 */
unsigned short AD5932_Registers [] = {
	0x0ED3,	//	Register 0  - Control bits.
	0x1190,	//	Register 1  - Number of increments.
	0x2D1B,	//	Register 2  - Lower 12 bits of delta frequency.
	0x3000,	//	Register 3  - Higher 12 bits of delta frequency.
	0x400A,	//	Register 4  - Increment interval 1.
	0x500A,	//	Register 5  - Increment interval 2.
	0x600A,	//	Register 6  - Increment interval 3.
	0x700A,	//	Register 7  - Increment interval 4.
	0x8000,	//	Register 8  - Reserved.
	0x9000,	//	Register 9  - Reserved.
	0xA000,	//	Register 10 - Reserved.
	0xB000,	//	Register 11 - Reserved.
	0xCD70,	//	Register 12 - Lower 12 bits of start frequency.
	0xD0A3,	//	Register 13 - Higher 12 bits of start frequency.
	0xE000,	//	Register 14 - Reserved.
	0xF000,	//	Register 15 - Reserved.
};
/**********************************************************************
 *			FUNCTIONS
 ***********************************************************************/
/**
 *	@brief		Установка регистра.
 *	@param[in]	usRegister - Значение регистра.
 *	@reetval	NONE.
 */
void AD5932_Register ( uint16_t usRegister )
{
	AD5932_Registers [ (int)((usRegister >> 12) & 0x0F) ] = usRegister;
}

/**
 *	@brief		Инициализация перифирии для работы с микросхемой AD5932.
 *	@param[in]	fsync_port, fsync_pin - Порт и Пин для управления линией FSYNC.
 *	@param[in]	standby_port, standby_pin - Порт и Пин для управления линией STANDBY.
 *	@param[in]	hspi - Указатель на SPI для управления микросхемой AD5932.
 *	@retval		NONE
 */
void AD5932_SetPeriph ( 
				GPIO_TypeDef* fsync_port,	uint16_t fsync_pin, 
				GPIO_TypeDef* standby_port,	uint16_t standby_pin,
				GPIO_TypeDef* intpt_port,	uint16_t intpt_pin,
				SPI_HandleTypeDef* 	hspi )
{
	AD5932_Periph.hspi = hspi;
	
	AD5932_Periph.fsync_port	= fsync_port;
	AD5932_Periph.fsync_pin		= fsync_pin;
	
	AD5932_Periph.standby_port	= standby_port;
	AD5932_Periph.standby_pin	= standby_pin;	

	AD5932_Periph.intpt_port	= intpt_port;
	AD5932_Periph.intpt_pin		= intpt_pin;	
}

/**
 *	@brief		Запись регистра в микросхемы AD5932.
 *	@param[in]	usRegister - Регистр микросхемы AD5932.
 *	@retval		NONE.
 */
void AD5932_SetRegister ( uint16_t usRegister )
{
	int index=0;
	unsigned char data[2];

	index = (int)((usRegister >> 12) & 0x0F);

	AD5932_Registers [ index ] = usRegister;
	data[0] = ( unsigned char )( ( usRegister >> 8 ) & 0xFF );
	data[1] = ( unsigned char )( ( usRegister >> 0 ) & 0xFF );
	
	HAL_GPIO_WritePin ( AD5932_Periph.fsync_port, AD5932_Periph.fsync_pin, GPIO_PIN_RESET );
	HAL_Delay (1);
	HAL_SPI_Transmit  ( AD5932_Periph.hspi, data, 2, 100 );
	HAL_Delay (1);
	HAL_GPIO_WritePin ( AD5932_Periph.fsync_port, AD5932_Periph.fsync_pin, GPIO_PIN_SET );
}

/**
 *	@brief	Функция записи регистра.
 *	@retval	NONE
 */
void AD5932_WriteRegister ( uint16_t usRegister )
{
	unsigned char data[2];

	data[0] = ( unsigned char )( ( usRegister >> 8 ) & 0xFF );
	data[1] = ( unsigned char )( ( usRegister >> 0 ) & 0xFF );
	
	HAL_GPIO_WritePin ( AD5932_Periph.fsync_port, AD5932_Periph.fsync_pin, GPIO_PIN_RESET );
	HAL_Delay (1);
	HAL_SPI_Transmit  ( AD5932_Periph.hspi, data, 2, 100 );
	HAL_Delay (1);
	HAL_GPIO_WritePin ( AD5932_Periph.fsync_port, AD5932_Periph.fsync_pin, GPIO_PIN_SET );
}

/**
 *	@brief	Функция считывания регистра AD5932
 *	@param [in] index - индекс регистра.
 *	@retval	Регистр.
 */
unsigned short AD5932_GetRegister ( int index )
{
	return AD5932_Registers [ index & 0x0F ];
}

/**
 *	@brief	Функция инициализации микросхемы AD5932
 *	@retval	NONE.
 */
void AD5932_Init ( void )
{
	// Для подстраховки устанавливаю линию FSYNC в состояние HIGH.
	HAL_GPIO_WritePin ( AD5932_Periph.fsync_port,	AD5932_Periph.fsync_pin,	GPIO_PIN_SET );
	HAL_Delay (100);
	
	//	Включение микросхемы.
	HAL_GPIO_WritePin ( AD5932_Periph.standby_port, AD5932_Periph.standby_pin,	GPIO_PIN_RESET );
	HAL_Delay (100);

	//	Включение микросхемы.
	HAL_GPIO_WritePin ( AD5932_Periph.intpt_port,	AD5932_Periph.intpt_pin,	GPIO_PIN_RESET );
	HAL_Delay (100);	
	
	//	Отгрузка микросхемы.
	
	AD5932_WriteRegister ( AD5932_Registers [ AD5932_REGISTER0  ] ); HAL_Delay (10);	
	AD5932_WriteRegister ( AD5932_Registers [ AD5932_REGISTER1  ] ); HAL_Delay (10);
	AD5932_WriteRegister ( AD5932_Registers [ AD5932_REGISTER2  ] ); HAL_Delay (10);
	AD5932_WriteRegister ( AD5932_Registers [ AD5932_REGISTER3  ] ); HAL_Delay (10);	
	AD5932_WriteRegister ( AD5932_Registers [ AD5932_REGISTER6  ] ); HAL_Delay (10);	
	AD5932_WriteRegister ( AD5932_Registers [ AD5932_REGISTER12 ] ); HAL_Delay (10);
	AD5932_WriteRegister ( AD5932_Registers [ AD5932_REGISTER13 ] ); HAL_Delay (10);
	AD5932_WriteRegister ( AD5932_Registers [ AD5932_REGISTER0  ] ); HAL_Delay (10);
}

/********************** END OF FILE ***********************************/
