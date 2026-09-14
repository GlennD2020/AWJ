/**
 *	@file:	detector.c
 *  @date:	07/01/2021
 *	@author:	Kirillov A.V.
 */
#define __C_DETECTOR_

/****************************************************************************
 *							INCLUDES										*
 ****************************************************************************/
#include "detector.h"

/****************************************************************************
 *							PERIPHERIAL										*
 ****************************************************************************/
extern ADC_HandleTypeDef hadc1;

/****************************************************************************
 *						STATIC VARIABLES									*
 ****************************************************************************/
static uint16_t det_meas_result = 0;

/****************************************************************************
 *						CALLBACK FUNCTIONS									*
 ****************************************************************************/
/**
 *	@brief	HAL_ADC_ConvCpltCallback
 */
void HAL_ADC_ConvCpltCallback			( ADC_HandleTypeDef* hadc )
{
	dms_meas_volt_done = 1;
}

/****************************************************************************
 *							FUNCTIONS										*
 ****************************************************************************/
/**
 *	@brief 	Получение усредненного значения.
 *	@param 	average - Степень усреднения.
 *	@return	Усреднённое значение.
 */
uint16_t get_average_voltage		( uint16_t average )
{
	uint32_t adc = 0;
	for ( int i=0; i<average; i++ ) {
		adc = adc + adc_buffer[i];
	}
	adc = (uint16_t)(adc/average);
	return adc;
}

/**
 * @brief	Измерение напряжения
 * @param	ctrl - Статус измерения.
 * @return	Результат измерений.
 */
uint16_t detector_measure	( uint16_t ctrl )
{
	if ( ctrl==1 )
	{
		if ( dms_meas_volt_run==0 )
		{
			dms_meas_volt_run = 1;
			dms_meas_volt_done = 0;
			HAL_ADC_Start_DMA ( &hadc1, (uint32_t*)adc_buffer, DETECTOR_AVERAGE_DEFAULT );
		}
		else
		{
			if ( dms_meas_volt_done==1 )
			{
				HAL_ADC_Stop_DMA ( &hadc1 );
				det_meas_result = get_average_voltage ( DETECTOR_AVERAGE_DEFAULT );
				dms_meas_volt_done = 0;
				dms_meas_volt_run  = 0;
			}
		}
	}
	else
	{
		dms_meas_volt_done	= 0;
		dms_meas_volt_run  	= 0;
		det_meas_result	= 0;
	}
	return det_meas_result;
}
