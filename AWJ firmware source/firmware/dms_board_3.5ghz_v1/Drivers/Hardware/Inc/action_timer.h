/**
 *	@file:		action_timer.h
 *  @date:		20/03/2021
 *  @author:	Kirillov A.V.
 */
#ifndef __H_ACTION_TIMER_
#define __H_ACTION_TIMER_

/****************************************************************************
 *				INCLUDES													*
 ****************************************************************************/
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "stm32f4xx_hal.h"

/****************************************************************************
 *				DEFINES													    *
 ****************************************************************************/
#ifdef __C_ACTION_TIMER
	#define ACTION_TIMER_EXT
	#define ACTION_TIMER_VOL volatile
#else
	#define ACTION_TIMER_EXT extern
	#define ACTION_TIMER_VOL extern
#endif	///	__C_SWEEP_TIMER

/****************************************************************************
 *				TYPEDEF STRUCTS											    *
 ****************************************************************************/
typedef struct
{
	int*				pStatus;
	uint32_t*			pTimerCnt;
	uint32_t*			pTimerIrq;
	TIM_HandleTypeDef*	hTim;
}tsActionTimer;

/****************************************************************************
 *				FUNCTIONS												    *
 ****************************************************************************/
/**
 *	@brief	Инициализация таймера удержания частота.
 *	@param	timer - Указатель на таймер.
 *	@param	hold_time - Задержка частоты.
 *	@retval	NONE.
 */
void action_timer_init ( tsActionTimer* timer, float hold_time );

/**
 *	@brief 	Запуск таймера росчерка.
 *	@param	timer - Указатель на структуру таймера.
 *	@param	time - Время работы таймера
 *	@retval	NONE
 */
void action_timer_start	( tsActionTimer* timer, float time );

/**
 *	@brief	Остановка таймера росчерка.
 *	@param	timer - Указатель на структуру таймера.
 *	@retval	NONE.
 */
void action_timer_stop	( tsActionTimer* timer );

/**
 *	@brief	Состояние таймера росчерка.
 *	@retval	Состояние таймера.
 */
uint32_t action_timer_state	( tsActionTimer* timer );

#endif	//	__H_ACTION_TIMER_
