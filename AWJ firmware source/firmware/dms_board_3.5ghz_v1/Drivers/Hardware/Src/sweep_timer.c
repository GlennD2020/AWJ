/**
 *	@file:		sweep_timer.c
 *	@date:		08/01/2021
 *  @author:	Kirillov A.V.
 */

#define __C_SWEEP_TIMER

/********************************************************************************
 *						INCLUDES												*
 ********************************************************************************/
#include "sweep_timer.h"

/********************************************************************************
 *					EXTERN FUNCTIONS											*
 ********************************************************************************/
extern void Error_Handler(void);

/********************************************************************************
 *						FUNCTIONS												*
 ********************************************************************************/
/**
 *	@brief	Инициализация таймера удержания частота.
 *	@param	htim - Указатель на таймер.
 *	@param	hold_time - Задержка частоты.
 *	@retval	NONE.
 */
void sweep_timer_init		( TIM_HandleTypeDef* htim, float hold_time )
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
	htim->Init.Prescaler		 = prescaler;
	htim->Init.CounterMode 		 = TIM_COUNTERMODE_UP;
	htim->Init.Period 			 = period;
	htim->Init.ClockDivision 	 = TIM_CLOCKDIVISION_DIV2;
	htim->Init.AutoReloadPreload = TIM_AUTORELOAD_PRELOAD_DISABLE;
	if ( HAL_TIM_Base_Init(htim)!=HAL_OK )
	{
		Error_Handler();
	}
	sClockSourceConfig.ClockSource	= TIM_CLOCKSOURCE_INTERNAL;
	if ( HAL_TIM_ConfigClockSource ( htim, &sClockSourceConfig )!=HAL_OK )
	{
		Error_Handler();
	}
	sMasterConfig.MasterOutputTrigger = TIM_TRGO_RESET;
	sMasterConfig.MasterSlaveMode	  = TIM_MASTERSLAVEMODE_DISABLE;
	if ( HAL_TIMEx_MasterConfigSynchronization ( htim, &sMasterConfig )!=HAL_OK )
	{
		Error_Handler();
	}
}

/**
 *	@brief 	Запуск таймера росчерка.
 *	@param	htim - Указатель на таймер.
 *	@param	time - Время работы таймера
 *	@retval	NONE
 */
void sweep_timer_start		( TIM_HandleTypeDef* htim, float time )
{
	sweep_timer_status	= 1;
	sweep_timer_cnt		= sweep_timer_irq;
	sweep_timer_init ( htim, time );
	HAL_TIM_Base_Start_IT ( htim );
}

/**
 *	@brief	Остановка таймера росчерка.
 *	@param	htim - Указатель на таймер.
 *	@retval	NONE.
 */
void sweep_timer_stop		( TIM_HandleTypeDef* htim )
{
	sweep_timer_status	= 0;
	HAL_TIM_Base_Stop_IT ( htim );
}

/**
 * 	@brief	
 */
int get_sweep_timer_status	( void )
{
	return sweep_timer_status;
}

/**
 *	@brief	Состояние таймера росчерка.
 *	@retval	Состояние таймера.
 */
uint32_t sweep_timer_state	( void )
{
	if ( sweep_timer_status==1 )
	{
		if (sweep_timer_cnt > sweep_timer_irq )
		{
			return (4294967296-sweep_timer_cnt)+sweep_timer_irq;
		}
		else
		{
			return sweep_timer_irq-sweep_timer_cnt;
		}
	}
	return 0;
}

/************************** END OF FILE *************************************/
