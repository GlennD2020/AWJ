/**
 *	@file:		sweep_timer.h
 *	@date:		08/01/2021
 *  @author:	Kirillov A.V.
 */
#ifndef __H_SWEEP_TIMER_
#define __H_SWEEP_TIMER_

/****************************************************************************
 *				INCLUDES													*
 ****************************************************************************/
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "stm32f4xx_hal.h"

/********************************************************************************
 *						DEFINES													*
 ********************************************************************************/
#ifdef __C_SWEEP_TIMER
	#define SWEEP_TIMER_EXT
	#define SWEEP_TIMER_VOL	volatile
#else
	#define SWEEP_TIMER_EXT	extern
	#define SWEEP_TIMER_VOL	extern
#endif	///	__C_SWEEP_TIMER

SWEEP_TIMER_EXT int	 sweep_timer_status;	//	Состояние таймера росчерка.
SWEEP_TIMER_EXT uint32_t sweep_timer_cnt;	//	Состояние счетчика на момент запуска таймера.
SWEEP_TIMER_VOL uint32_t sweep_timer_irq;	//	Счетчик инкрементируемый при срабатывании прерывания.

/********************************************************************************
 *						FUNCTIONS												*
 ********************************************************************************/
/**
 *	@brief	Инициализация таймера удержания частота.
 *	@param	htim - Указатель на таймер.
 *	@param	hold_time - Задержка частоты.
 *	@retval	NONE.
 */
void sweep_timer_init 	( TIM_HandleTypeDef* htim, float hold_time );

/**
 *	@brief Запуск таймера росчерка.
 *	@param	htim - Указатель на таймер.
 *	@param	time - Время работы таймера
 *	@retval	NONE.
 */
void sweep_timer_start	( TIM_HandleTypeDef* htim, float time );

/**
 *	@brief	Остановка таймера росчерка.
 *	@param	htim - Указатель на таймер.
 *	@retval	NONE.
 */
void sweep_timer_stop	( TIM_HandleTypeDef* htim );

/**
 *	@brief	Функция считывания статуса таймера.
 *	@retval	Статус состояния таймера.
 */
int get_sweep_timer_status ( void );

/**
 *	@brief
 *	@retval
 */
uint32_t sweep_timer_state	( void );

#endif	//	__H_SWEEP_TIMER_

/************************** END OF FILE *************************************/

