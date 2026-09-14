/**
 *	@file:		action_timer.c
 *  @date:		20/03/2021
 *  @author:	Kirillov A.V.
 */

#define __C_ACTION_TIMER_

/****************************************************************************
 *				INCLUDES													*
 ****************************************************************************/
#include "action_timer.h"

/****************************************************************************
 *					EXTERN FUNCTIONS										*
 ****************************************************************************/
extern void Error_Handler ( void );

/****************************************************************************
 *				FUNCTIONS													*
 ****************************************************************************/
/**
 *	@brief	Инициализация таймера удержания частота.
 *	@param	timer - Указатель на таймер.
 *	@param	hold_time - Задержка частоты.
 *	@retval	NONE.
 */
void action_timer_init ( tsActionTimer* timer, float hold_time )
{
	uint32_t prescaler = 45000;
	uint32_t period    = 1000;
	TIM_ClockConfigTypeDef	sClockSourceConfig	= {0};
	TIM_MasterConfigTypeDef	sMasterConfig		= {0};

	if ( hold_time<=0.001F )
	{
		prescaler = 45;
		period    = (uint32_t)( hold_time * 1000000.0F );
	}
	else
	{
		prescaler = 45000;
		period    = (uint32_t)( hold_time * 1000.0F );
	}
	timer->hTim->Init.Prescaler		 	= prescaler;
	timer->hTim->Init.CounterMode 		= TIM_COUNTERMODE_UP;
	timer->hTim->Init.Period 			= period;
	timer->hTim->Init.ClockDivision 	= TIM_CLOCKDIVISION_DIV2;
	timer->hTim->Init.AutoReloadPreload = TIM_AUTORELOAD_PRELOAD_DISABLE;
	if ( HAL_TIM_Base_Init(timer->hTim)!=HAL_OK )
	{
		Error_Handler();
	}
	sClockSourceConfig.ClockSource	= TIM_CLOCKSOURCE_INTERNAL;
	if ( HAL_TIM_ConfigClockSource ( timer->hTim, &sClockSourceConfig )!=HAL_OK )
	{
		Error_Handler();
	}
	sMasterConfig.MasterOutputTrigger = TIM_TRGO_RESET;
	sMasterConfig.MasterSlaveMode	  = TIM_MASTERSLAVEMODE_DISABLE;
	if ( HAL_TIMEx_MasterConfigSynchronization ( timer->hTim, &sMasterConfig )!=HAL_OK )
	{
		Error_Handler();
	}
}

/**
 *	@brief 	Запуск таймера росчерка.
 *	@param	timer - Указатель на структуру таймера.
 *	@param	time - Время работы таймера
 *	@retval	NONE
 */
void action_timer_start	( tsActionTimer* timer, float time )
{
	*(timer->pStatus) = 1;
	*(timer->pTimerCnt) = *(timer->pTimerIrq);
	action_timer_init ( timer, time );
	HAL_TIM_Base_Start_IT ( timer->hTim );
}

/**
 *	@brief	Остановка таймера росчерка.
 *	@param	timer - Указатель на структуру таймера.
 *	@retval	NONE.
 */
void action_timer_stop	( tsActionTimer* timer )
{
	*(timer->pStatus) = 0;
	HAL_TIM_Base_Stop_IT ( timer->hTim );
}

/**
 *	@brief	Состояние таймера росчерка.
 *	@retval	Состояние таймера.
 */
uint32_t action_timer_state	( tsActionTimer* timer )
{
	if ( *(timer->pStatus)==1 )
	{
		if ( *(timer->pTimerCnt)>*(timer->pTimerIrq) )
		{
			return (4294967296-*(timer->pTimerCnt))+*(timer->pTimerIrq);
		}
		else
		{
			return *(timer->pTimerIrq)-*(timer->pTimerCnt);
		}
	}
	return 0;
}
