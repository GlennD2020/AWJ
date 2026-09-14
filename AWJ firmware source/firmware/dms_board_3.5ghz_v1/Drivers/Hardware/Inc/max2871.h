/*****************************************************************************
 *	@file:			max2871.h
 *	@brief:			Заголовочный файл библиотеки для работы с MAX2871.
 *	@version:		v1.0
 *	@author:		Kirillov A.V.
 *	@date:			21/05/2019
 *****************************************************************************/
#ifndef __H_MAX2871_
#define __H_MAX2871_

/*****************************************************************************
 *		MCU INCLUDES
 *****************************************************************************/
#ifdef STM32F446xx
	#include "stm32f4xx_hal.h"
#endif	//	STM32F446xx

#ifdef STM32F205xx
	#include "stm32f2xx_hal.h"
#endif	//	STM32F205xx 

/*****************************************************************************
 *		INCLUDES
 *****************************************************************************/
#include "max2871_spec.h"

#include "math.h"
/*****************************************************************************
 *		DEFINES
 *****************************************************************************/
#ifdef __C_MAX2871_
	#define MAX2871_VAL
#else
	#define MAX2871_VAL extern
#endif
	
/*****************************************************************************
 *		STRUCTURES
 *****************************************************************************/
/**
 *	@remark	Типовая структура для задания переферии, необходимой для работы с микросхемой MAX2871.
 */
typedef struct
{
	uint16_t			cs_pin;			//	Номер вывода линии CS
	GPIO_TypeDef*		cs_port;		//	Указатель на структуру для работы с Портом ввода вывода
	uint16_t			ld_pin;			//	Номер вывода линии CS
	GPIO_TypeDef*		ld_port;		//	Указатель на структуру для работы с Портом ввода вывода	
	uint16_t			muxout_pin;		//	Номер вывода линии MUXOUT
	GPIO_TypeDef*		muxout_port;	//	Указатель на структуру для работы с Портом ввода вывода	
	SPI_HandleTypeDef* 	hspi;			//	Указатель на объект для работы с SPI
} 
tsMax2871_Periph;

/*****************************************************************************
 *		DEFINE VARIABLES
 *****************************************************************************/
MAX2871_VAL tsMax2871_Periph	Max2871_Periph;
MAX2871_VAL double				Max2871_RefFrequency;

/*****************************************************************************
 *		FUNCTIONS
 *****************************************************************************/
#ifdef __cplusplus
extern "C" {
#endif

/**
 *	@brief	Считывание статуса выхода.
 *	@retval	[0/1] - 
 */
uint16_t MAX2871_RfOutStatus	( void );
	
/**
 *	@brief 	Функция записи в массив регистров MAX2871.
 *	@retval	NONE
 */
void MAX2871_Register		( uint32_t ulRegister );
	
/**
 *	@brief	MAX2871_Init
 *	@remark	Инициализация микросхемы ФАПЧ MAX2871.
 *	@retval	NONE
 */	
void MAX2871_Init 			( void );

/**
 *	@brief	MAX2871_GetMuxOut
 *	@remark Опрос состояния вывода MUXOUT микросхемы MAX2871
 */
int MAX2871_GetMuxOut		( void );

/**
 *	@brief	MAX2871_GetLD
 *	@remark Опрос состояния вывода LD микросхемы MAX2871
 *	@retval	Состояние линии захвата: 
 *					[0] - Синтезатор не в захвате.
 *					[1] - Синтезатор в захвате.
 */	
int MAX2871_GetLD			( void );

/**
 *	@brief	MAX2871_Set_Periph
 *	@remark	Задание переферии для работы с микросхемой MAX2871.
 *	@param 	cs_port		- Порт линии CS.
 *	@param 	cs_pin 		- Номер вывода линии CS.
 *	@param 	ld_port 	- Порт линии LD.
 *	@param 	ld_pin 		- Номер вывода линии LD.
 *	@param 	muxout_port	- Порт линии MUXOUT.
 *	@param	muxout_pin	- Номер вывода линии MUXOUT.
 *	@param	hspi		- SPI для работы.
 *	@retval	NONE
 */			
void MAX2871_SetPeriph 		( 
				GPIO_TypeDef* cs_port, uint16_t cs_pin, 
				GPIO_TypeDef* ld_port, uint16_t ld_pin, 
				GPIO_TypeDef* muxout_port, uint16_t	muxout_pin,
				SPI_HandleTypeDef* 	hspi );

/**
 *	@brief	MAX2871_WriteRegister
 *	@remark	Функция записи регистра через интерфейс SPI.
 *	@param	ulRegister - Записываемый регистр.
 *	@retval	NONE
 */
void MAX2871_WriteRegister	( uint32_t ulRegister );

/**
 *	@brief	MAX2871_WriteRegister
 *	@remark	Функция записи регистра в массив MAX2871_Registers и запись в микросхему через интерфейс SPI.
 *	@param	ulRegister - Записываемый регистр.
 *	@retval	NONE
 */
void MAX2871_SetRegister 	( uint32_t ulRegister );

/**
 *	@brief	MAX2871_SetFrequency
 *	@param	fFrequency 	- Частота на выходе микросхемы.
 *	@param	waitLD 		- Флаг управления ожиданием захвата от ФАПЧ.
 *	@retval	Статус выполнения операции.
 */
int MAX2871_SetFrequency 	( double fFrequency, int waitLD );

/**
 *	@brief		Получение текущей частоты микросхемы.
 *	@return		Расчитанное значение частоты.
 */
double MAX2871_GetFrequency ( );

/**
 *	@brief		Функция выключения мощности.
 *	@return		NONE.
 */
void MAX2871_Off			( void );

/**
 *	@brief		Функция включения мощности.
 *	@return		NONE.
 */
void MAX2871_On				( void );

/**
 *	@brief		Считывание регистра.
 *	@param [in]	index - номер считываемого регистра.
 *	@retval		Регистр MAX2871.
 */
unsigned long MAX2871_GetRegister ( int index );

#ifdef __cplusplus
};
#endif

#endif	//	__H_MAX2871_
