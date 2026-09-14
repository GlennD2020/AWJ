/**
 *	@file:		pulse_timer.h
 *  @date:		18/01/2021
 *	@author:	Kirillov A.V.
 */
#ifndef __H_PULSE_TIMER_
#define __H_PULSE_TIMER_

/****************************************************************************
 *					INCLUDES												*
 ****************************************************************************/
#ifdef STM32F446xx
	#include "stm32f4xx_hal.h"
#endif	//	STM32F446xx
#ifdef STM32F205xx
	#include "stm32f2xx_hal.h"
#endif	//	STM32F205xx

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

/****************************************************************************
 *		            DEFINES                                                 *
 ****************************************************************************/
#define PULSE_TIMER_CLK		80e6
#define	PULSE_TIMER_ON		1
#define	PULSE_TIMER_OFF		0

/****************************************************************************
 *					FUNCTIONS												*
 ****************************************************************************/
/**
 *	@brief		Настройка таймера для формирования импульсов.
 *	@brief [in] htim			- Указатель на таймер.
 *	@param [in]	tim_channel		- Канал таймера.
 *	@param [in]	tim_prescaler 	- Прескалер таймера.
 *	@param [in]	tim_pulse		- Длительность импульса.
 *	@param [in]	tim_period		- Период импульса.
 *	@retval		NONE.
 */
void pulse_timer_init ( TIM_HandleTypeDef *htim, uint32_t channel, uint32_t prescaler, uint32_t pulse, uint32_t period );
/**
 *	@brief	Настройка таймера для генерации импульсной последовательности.
 *	@param [in]	htim 		- Указатель на таймер, который используется для генерации импульсной последовательности.
 *	@param [in]	TIM_Channel - Канал таймера.
 *	@param [in] frequency	- Частота импульсной последовательности в Гц.
 *	@param [in] duration	- Скажность импульсной последовательности.
 *  @retval NONE.
 */
void pulse_timer_ctrl ( TIM_HandleTypeDef *htim, uint32_t TIM_Channel, int pulse_ctrl, float frequency, float duration );

#endif	//	__H_PULSE_TIMER_
