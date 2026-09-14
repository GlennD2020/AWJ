/**
 *	@file:		detector.h
 *  @date:		07/01/2021
 *	@author:	Kirillov A.V.
 */

#ifndef __H_DETECTOR_
#define __H_DETECTOR_

/****************************************************************************
 *				INCLUDES													*
 ****************************************************************************/
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "stm32f4xx_hal.h"

/****************************************************************************
 *				DEFINES														*
 ****************************************************************************/
#ifdef __C_DETECTOR_
	#define DET_EXT
	#define DET_VOL	volatile
#else
	#define DET_EXT	extern
	#define DET_VOL	extern
#endif	///	__C_DETECTOR_

#define DETECTOR_SIZE_BUFF			256
#define DETECTOR_AVERAGE_DEFAULT	128
#define DETECTOR_AVERAGE_SWEEP		32

/****************************************************************************
 *				VARIABLES													*
 ****************************************************************************/
/** @brief Буфер для измерения входного напряжения.							*/
DET_EXT uint16_t adc_buffer [DETECTOR_SIZE_BUFF];
DET_VOL int 	 dms_meas_volt_done;	//< Флаг завершения измерение напряжения детектора.
DET_VOL int 	 dms_meas_volt_run;		//< Флаг завершения измерение напряжения детектора.

/****************************************************************************
 *				FUNCTIONS													*
 ****************************************************************************/
/**
 *	@brief 	Получение усредненного значения.
 *	@param 	average - Степень усреднения.
 *	@return	Усреднённое значение.
 */
uint16_t get_average_voltage		( uint16_t average );

/**
 * @brief	Измерение напряжения
 * @param	control - Статус измерения.
 * @return	Результат измерений.
 */
uint16_t detector_measure			( uint16_t control );

#endif //	__H_DETECTOR_

/************************** END OF FILE *************************************/
