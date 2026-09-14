/***********************************************************************
 *	@file:		ad5932.h
 *	@brief:		Заголовочный файл библиотеки для работы с AD5932.
 *	@version:	v1.0
 *	@author:	Kirillov A.V.
 *	@date:		15/12/2019
 ***********************************************************************/
#ifndef __H_AD5932_
#define __H_AD5932_

/************************************************************************
 *			MCU INCLUDES
 ***********************************************************************/
#ifdef STM32F446xx
	#include "stm32f4xx_hal.h"
#endif	//	STM32F446xx

#ifdef STM32F205xx
	#include "stm32f2xx_hal.h"
#endif	//	STM32F205xx 

/***********************************************************************
 *			INCLUDES
 ***********************************************************************/
#include "ad5932_spec.h"
/***********************************************************************
 *			DEFINES
 ***********************************************************************/
#ifdef __C_AD5932_
	#define AD5932_VAL
#else
	#define AD5932_VAL extern
#endif
	
/************************************************************************
 *			TYPEDEF STRUCT
 ***********************************************************************/
typedef struct
{
	uint16_t			fsync_pin;		//	Номер вывода линии FSYNC.
	GPIO_TypeDef*		fsync_port;		//	Указатель на структуру для работы с Портом ввода вывода.
	
	uint16_t			standby_pin;	//	Номер вывода линии STANDBY.
	GPIO_TypeDef*		standby_port;	//	Указатель на структуру для работы с Портом ввода вывода.
	
	uint16_t			ctrl_pin;		//	Номер вывода линии CTRL.
	GPIO_TypeDef*		ctrl_port;		//	Указатель на структуру для работы с Портом ввода вывода.

	uint16_t			intpt_pin;		//	Номер вывода линии INTERRUPT
	GPIO_TypeDef*		intpt_port;		//	Указатель на структуру для работы с Портом ввода вывода.
	
	SPI_HandleTypeDef* 	hspi;			//	Указатель на объект для работы с SPI.
}
tsAD5932_Periph;

/***********************************************************************
 *			VARIABLES
 ***********************************************************************/
AD5932_VAL tsAD5932_Periph	AD5932_Periph;	//	Периферия для работы с AD5932

/************************************************************************
 *			FUNCTIONS
 ***********************************************************************/
#ifdef __cplusplus
extern "C" {
#endif

/**
 *	@brief		Установка регистра.
 *	@param[in]	usRegister - Значение регистра.
 *	@reetval	NONE.
 */
void AD5932_Register ( uint16_t usRegister );
	
/**
 *	@brief		Инициализация перифирии для работы с микросхемой AD5932.
 *	@param[in]	fsync_port, fsync_pin - Порт и Пин для управления линией FSYNC.
 *	@param[in]	standby_port, standby_pin - Порт и Пин для управления линией STANDBY.
 *	@param[in]	hspi - Указатель на SPI для управления микросхемой AD5932.
 *	@retval		NONE
 */
void AD5932_SetPeriph ( GPIO_TypeDef* fsync_port,	uint16_t fsync_pin, 
						GPIO_TypeDef* standby_port,	uint16_t standby_pin,
						GPIO_TypeDef* intpt_port,	uint16_t intpt_pin,
						SPI_HandleTypeDef* 	hspi );

/**
 *	@brief		Запись регистра в микросхемы AD5932.
 *	@param[in]	usRegister - Регистр микросхемы AD5932.
 *	@retval		NONE.
 */
void AD5932_SetRegister ( uint16_t usRegister );

/**
 *	@brief		Инициализация микросхемы AD5932.
 *	@retval		NONE
 */
void AD5932_Init ( void );

/**
 *	@brief	Функция считывания регистра AD5932
 *	@param [in] index - индекс регистра.
 *	@retval	Регистр.
 */
unsigned short AD5932_GetRegister ( int index );

#ifdef __cplusplus
};
#endif
	
#endif	//	__H_AD5932_
 
/********************** END OF FILE ************************************/
