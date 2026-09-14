/***************************************************************************
 * @file:	ad9106.c
 * @date:	15/12/2020
 * @author: Kirillov A.V.
 ***************************************************************************/

#define __C_AD9106_

/***************************************************************************
 *			INCLUDES
 ***************************************************************************/
#include "ad9106.h"

/***************************************************************************
 *			VARIABLES
 ***************************************************************************/
/**
 *	@brief Массив регистров.
 */
static unsigned short ad9106_register[128] = 
{
	#include "ad9106_config1.h"
};

/***************************************************************************
 *			FUNCTIONS
 ***************************************************************************/
/**
 *	@brief		Инициализация перифирии для работы с микросхемой AD9106.
 *	@param[in]	hspi - Указатель на SPI для управления микросхемой AD9106.
 *	@param[in]	reset_port, reset_pin - Порт и Пин для управления линией RESET.
 *	@param[in]	ctrl_port,  ctrl_pin - Порт и Пин для управления линией CTRL.
 *	@param[in]	ctrl_port,  ctrl_pin - Порт и Пин для управления линией CS.
 *	@retval		NONE
 */
void AD9106_SetPeriph (
	SPI_HandleTypeDef* 	hspi,
	GPIO_TypeDef* reset_port, 	unsigned short reset_pin,
	GPIO_TypeDef* trigger_port,	unsigned short trigger_pin,
	GPIO_TypeDef* cs_port, 		unsigned short cs_pin )
{
	AD9106_Periph.hspi 		 	= hspi;

	AD9106_Periph.reset_port 	= reset_port;
	AD9106_Periph.reset_pin	 	= reset_pin;

	AD9106_Periph.trigger_port	= trigger_port;
	AD9106_Periph.trigger_pin	= trigger_pin;
	
	AD9106_Periph.cs_port	 	= cs_port;
	AD9106_Periph.cs_pin	 	= cs_pin;
}

/**
 *	@brief	AD9106_Init
 *	@remark	Инициализация микросхемы AD9106.
 *	@retval	NONE
 */	
void AD9106_Init ( void )
{
	HAL_GPIO_WritePin ( AD9106_Periph.cs_port, AD9106_Periph.cs_pin, GPIO_PIN_SET );

	AD9106.reference = 170000000.0;

	AD9106_WriteRegister ( 0x0000, 0x0000 );

	for ( unsigned short i=0; i<0x60; i++ ) {
		AD9106_WriteRegister ( ad9106_register[i], i );
	}

	AD9106_WriteRegister ( 0x0001, 0x001E );
	AD9106_WriteRegister ( 0x0001, 0x001D );
}

/**
 *	@brief	AD9106_Default
 *	@remark	Установка DDS в состояние заданное в файле [ad9106_config1.h].
 *	@retval	NONE
 */
void AD9106_Default ( void )
{
	unsigned short ad9106_def_register[128] =
	{
		#include "ad9106_config1.h"
	};

	AD9106.reference = 170000000.0;
	AD9106_WriteRegister ( 0x0000, 0x0000 );

	for ( unsigned short i=0; i<0x60; i++ )
	{
		ad9106_register[i] = ad9106_def_register[i];
		AD9106_WriteRegister ( ad9106_register[i], i );
	}
	AD9106_WriteRegister ( 0x0001, 0x001E );
	AD9106_WriteRegister ( 0x0001, 0x001D );
}

/**
 *	@brief		Запись регистра в микросхемы AD9106.
 *	@param[in]	usRegister - Регистр микросхемы AD9106.
 *	@param[in]	address	   - Адрес регистра.
 *	@retval		NONE.
 */
void AD9106_WriteRegister ( unsigned short usRegister, unsigned short address )
{
	unsigned char data[4];
	
	ad9106_register[address] = usRegister;

	data[0] = ( unsigned char )( ( address 	  >> 8 ) & 0xFF );
	data[1] = ( unsigned char )( ( address 	  >> 0 ) & 0xFF );
	data[2] = ( unsigned char )( ( usRegister >> 8 ) & 0xFF );
	data[3] = ( unsigned char )( ( usRegister >> 0 ) & 0xFF );
	
	HAL_GPIO_WritePin ( AD9106_Periph.cs_port, AD9106_Periph.cs_pin, GPIO_PIN_RESET );
	HAL_SPI_Transmit  ( AD9106_Periph.hspi, data, 4, 100 );
	HAL_GPIO_WritePin ( AD9106_Periph.cs_port, AD9106_Periph.cs_pin, GPIO_PIN_SET );
}

/**
 *	@brief		Считывание значения регистра из внутреннего массива. 
 *	@param[in]	address 	- Адрес регистра.
 *	@retval		NONE.
 */
uint16_t AD9106_GetRegister ( unsigned short address )
{
	return ad9106_register[address];
}

/**
 *	@brief		Установка значения регистра.
 *	@param[in]	address -	Адрес регистра.
 *	@param[in]	reg -		Данные регистра.
 *	retval		NONE
 */
void AD9106_SetRegister ( int address, unsigned short reg )
{
	ad9106_register[address] = reg;
}

/**
 *	@brief		Получение текущей частоты ДДС.
 *	@retval		Текущая частота.
 */
float AD9106_CurrentFrequency ( )
{
	uint32_t ftw = ((uint32_t)AD9106_GetRegister(0x3E))<<8;
	float value = 0.0F;

	ftw = ftw + (((uint32_t)(AD9106_GetRegister(0x3F)>>8))&0x000000FFUL);
	value = (float)ftw;
	value = value/16777216.0;
	value = value*AD9106.reference;

	return value;
}

/**
 * @brief	Установка выходной частоты микросхемы DDS.
 * @retval	NONE.
 */
void AD9106_SetFrequency ( float freq )
{
	uint32_t FTW = 0;
	float fFTW   = freq/AD9106.reference;

	fFTW = fFTW * 16777216.0;
	FTW  = (uint32_t)fFTW;

	AD9106_WriteRegister ( (FTW&0xFFFF00)>>8, 0x3E );
	AD9106_WriteRegister ( (FTW&0x0000FF)<<8, 0x3F );
	AD9106_WriteRegister ( 0x0001, 0x001D );
}

/**
 * @brief		Запись данных для перестройки в режиме RAMP.
 * @param[in]	MaxRampValue -	Максимальное значение ЦАПа.
 * @param[in]	Count - 		Кол-во точек росчерка.
 * @retval		NONE
 */
void AD9106_RampConfig_WriteToMemory ( uint16_t MaxRampValue, uint16_t Count )
{
	float fStepRamp = ( (float)MaxRampValue/((float)Count-1.0) );

	AD9106_WriteRegister ( 0x0000, 0x1F );
	AD9106_WriteRegister ( 0x0000, 0x1E );
	AD9106_WriteRegister ( 0x0001, 0x1D );

	AD9106_WriteRegister ( 0x0004, 0x1E );
	AD9106_WriteRegister ( 0x0001, 0x1D );

	for ( uint16_t i=0; i<Count; i++ )
	{
		uint16_t point = (uint16_t)( fStepRamp * (float)i );
		point = (uint16_t)(point & 0x0FFF);
		AD9106_WriteRegister ( (uint16_t)(point << 4), (uint16_t)(0x6002 + i) );
	}

	AD9106_WriteRegister ( 0x0001, 0x1D );

	AD9106_WriteRegister ( 0x0000, 0x1E );
	AD9106_WriteRegister ( 0x0001, 0x1D );
}

/************************** END OF FILE *************************************/
