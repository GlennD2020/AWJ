/****************************************************************************
 * @file:	ad9106.h
 * @date:	15/12/2020
 * @author: Kirillov A.V.
 ***************************************************************************/
#ifndef __H_AD9106_
#define __H_AD9106_

/***************************************************************************
 *			MCU INCLUDES
 ***************************************************************************/
#ifdef STM32F446xx
	#include "stm32f4xx_hal.h"
#endif	//	STM32F446xx

#ifdef STM32F205xx
	#include "stm32f2xx_hal.h"
#endif	//	STM32F205xx 

/***************************************************************************
 *			INCLUDES
 ***************************************************************************/
#include "ad9106_spec.h"

/***************************************************************************
 *			DEFINES
 ***************************************************************************/
#ifdef __C_AD9106_
	#define AD9106_VAL
#else
	#define AD9106_VAL extern
#endif

#define AD9106_CONTINUE	0
#define AD9106_SOFTWARE	1
#define AD9106_RAMP		2
#define AD9106_PSEUDO	3

/***************************************************************************
 *			TYPEDEF STRUCT
 ***************************************************************************/
typedef struct
{
	uint16_t			cs_pin;
	GPIO_TypeDef*		cs_port;
	
	uint16_t			reset_pin;
	GPIO_TypeDef*		reset_port;
	
	uint16_t			trigger_pin;
	GPIO_TypeDef*		trigger_port;
	
	SPI_HandleTypeDef* 	hspi;

}tsAD9106_Periph;

/**
 * @brief	Структура настройки ДДС.
 */
typedef struct
{
	uint16_t		sweep_ctrl;	//<	Управление свипа.
	uint32_t		points;		//<	Кол-во точек.
	float			ctrl_freq;	//<	Управляющая частота.
	float			reference;	//< Опорная частота.
	float			current;	//< Текущая частота.
	float			begin;		//<	Начальная частота.
	float			step;		//< Шаг изменения частоты.
}tsAD9106;

/***************************************************************************
 *			VARIABLES
 ***************************************************************************/
/**
 *	@brief Настройка периферии для работы с AD9106.
 */
AD9106_VAL	tsAD9106_Periph	AD9106_Periph;
/**
 *	@brief Настройка микросхемы ДДС.
 */
AD9106_VAL	tsAD9106 AD9106;

/***************************************************************************
 *			FUNCTIONS
 ***************************************************************************/
#ifdef __cplusplus
extern "C" {
#endif

/**
 *	@brief		Инициализация перифирии для работы с микросхемой AD9106.
 *	@param[in]	hspi - Указатель на SPI для управления микросхемой AD9106.
 *	@param[in]	reset_port, reset_pin - Порт и Пин для управления линией RESET.
 *	@param[in]	trigger_port,  trigger_pin - Порт и Пин для управления линией TRIGGER.
 *	@param[in]	cs_port,  cs_pin - Порт и Пин для управления линией CS.
 *	@retval		NONE
 */
void AD9106_SetPeriph (
	SPI_HandleTypeDef* 	hspi,
	GPIO_TypeDef* reset_port, 	uint16_t reset_pin,
	GPIO_TypeDef* trigger_port,	uint16_t trigger_pin,
	GPIO_TypeDef* cs_port, 		uint16_t cs_pin );

/**
 *	@brief	AD9106_Init
 *	@remark	Инициализация микросхемы AD9106.
 *	@retval	NONE
 */	
void AD9106_Init ( void );

/**
 *	@brief	AD9106_Default
 *	@remark	Установка DDS в состояние заданное в файле [ad9106_config1.h].
 *	@retval	NONE
 */
void AD9106_Default ( void );

/**
 *	@brief		Запись регистра в микросхемы AD9106.
 *	@param[in]	usRegister	- Регистр микросхемы AD9106.
 *	@param[in]	address		- Адрес регистра.
 *	@retval		NONE.
 */
void AD9106_WriteRegister ( unsigned short usRegister, unsigned short address );

/**
 *	@brief		Считывание значения регистра из внутреннего массива. 
 *	@param[in]	address 	- Адрес регистра.
 *	@retval		Значение регистра.
 */
unsigned short AD9106_GetRegister ( unsigned short address );

/**
 *	@brief		Установка значения регистра.
 *	@param[in]	address -	Адрес регистра.
 *	@param[in]	reg -		Данные регистра.
 *	retval		NONE
 */
void AD9106_SetRegister ( int address, unsigned short reg );

/**
 *	@brief	Получение текущей частоты ДДС.
 *	@retval	Текущая частота.
 */
float AD9106_CurrentFrequency ( );

/**
 * @brief Установка частоты.
 * @retval	NONE
 */
void AD9106_SetFrequency ( float freq );

/**
 * @brief		Запись данных для перестройки в режиме RAMP.
 * @param[in]	MaxRampValue -	Максимальное значение ЦАПа.
 * @param[in]	Count - 		Кол-во точек росчерка.
 * @retval		NONE
 */
void AD9106_RampConfig_WriteToMemory ( uint16_t MaxRampValue, uint16_t Count );

#ifdef __cplusplus
};
#endif

#endif ///	__H_AD9106_

/************************** END OF FILE ************************************/
