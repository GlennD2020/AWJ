/**
 *	@file:		pulse_timer.c
 *  @date:		18/01/2021
 *	@author:	Kirillov A.V.
 */
#define __C_PULSE_TIMER_

/****************************************************************************
 *					INCLUDES												*
 ****************************************************************************/
#include <pulse_timer.h>

/****************************************************************************
 *				EXTERNAL FUNCTIONS											*
 ****************************************************************************/
extern void Error_Handler ( void );
extern void HAL_TIM_MspPostInit ( TIM_HandleTypeDef* htim );

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
void pulse_timer_init ( TIM_HandleTypeDef *htim, uint32_t channel, uint32_t prescaler, uint32_t pulse, uint32_t period )
{
	TIM_ClockConfigTypeDef	sClockSourceConfig;
	TIM_MasterConfigTypeDef	sMasterConfig;
	TIM_OC_InitTypeDef 		sConfigOC;

	htim->Init.Prescaler 		 = prescaler;
	htim->Init.CounterMode 		 = TIM_COUNTERMODE_UP;
	htim->Init.Period 			 = period-1;
	htim->Init.ClockDivision 	 = TIM_CLOCKDIVISION_DIV1;
	htim->Init.AutoReloadPreload = TIM_AUTORELOAD_PRELOAD_DISABLE;
	if ( HAL_TIM_Base_Init(htim) != HAL_OK )
	{
		Error_Handler();
	}
	sClockSourceConfig.ClockSource = TIM_CLOCKSOURCE_INTERNAL;
	if ( HAL_TIM_ConfigClockSource(htim, &sClockSourceConfig) != HAL_OK )
	{
		Error_Handler();
	}
	if ( HAL_TIM_PWM_Init(htim) != HAL_OK )
	{
		Error_Handler();
	}
	sMasterConfig.MasterOutputTrigger = TIM_TRGO_RESET;
	sMasterConfig.MasterSlaveMode	  = TIM_MASTERSLAVEMODE_DISABLE;
	if ( HAL_TIMEx_MasterConfigSynchronization(htim, &sMasterConfig) != HAL_OK )
	{
		Error_Handler();
	}
	sConfigOC.OCMode 	 = TIM_OCMODE_PWM1;
	sConfigOC.Pulse 	 = pulse-1;
	sConfigOC.OCPolarity = TIM_OCPOLARITY_HIGH;
	sConfigOC.OCFastMode = TIM_OCFAST_DISABLE;
	if ( HAL_TIM_PWM_ConfigChannel (htim, &sConfigOC, channel) != HAL_OK )
	{
		Error_Handler();
	}
	HAL_TIM_MspPostInit(htim);
}

/**
 *	@brief	Настройка таймера для генерации импульсной последовательности.
 *	@param [in]	htim 		- Указатель на таймер, который используется для генерации импульсной последовательности.
 *	@param [in]	TIM_Channel - Канал таймера.
 *	@param [in] frequency	- Частота импульсной последовательности в Гц.
 *	@param [in] duration	- Скажность импульсной последовательности.
 *  @retval NONE.
 */
void pulse_timer_ctrl ( TIM_HandleTypeDef *htim, uint32_t TIM_Channel, int pulse_ctrl, float frequency, float duration )
{
	uint32_t ulDuration = (uint32_t)(duration*1000.0);
	uint32_t period     = 0;
	uint32_t pulse      = 0;
	uint32_t prescaler  = 0;
	uint32_t N          = 0;
	
	N         = PULSE_TIMER_CLK/frequency;
	prescaler = N/65535;
	period    = N - (prescaler*65535);
	pulse     = (period*ulDuration)/1000;
	
	if ( pulse_ctrl==0 )
	{
		HAL_TIM_PWM_Stop ( htim, TIM_Channel );
	}
	else if ( pulse_ctrl==1 )
	{
		pulse_timer_init  ( htim, TIM_Channel, prescaler, pulse, period );
		HAL_TIM_PWM_Start ( htim, TIM_Channel );
	}
}
