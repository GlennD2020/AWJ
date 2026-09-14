/****************************************************************************
 * @file:		dms_functions.h												*
 * @project:	DMS 3.5...3.8 GHz											*
 * @author:		Kirillov A.V.												*
 ****************************************************************************/
#ifndef __H_DMS_FUNCTIONS_
#define __H_DMS_FUNCTIONS_

/****************************************************************************
 *					INCLUDES												*
 ****************************************************************************/
#ifdef STM32F446xx
	#include "stm32f4xx_hal.h"
#endif	//	STM32F446xx

#ifdef STM32F205xx
	#include "stm32f2xx_hal.h"
#endif	//	STM32F205xx

#include "main.h"
#include "syscom.h"
#include "crc16.h"

#include "dms_device.h"
#include "pulse_timer.h"
#include "sweep_timer.h"
#include "action_timer.h"

#include "max2871.h"
#if DMS_USE_AD5932==1
	#include "ad5932.h"
#else
	#if DMS_USE_AD9106==1
		#include "ad9106.h"
	#endif
#endif
#include "at24c32d.h"
#include "dms_tdm.h"

/****************************************************************************
 *							DEFINES											*
 ****************************************************************************/
#ifdef __C_DMS_FUNCTIONS_
	#define DMS_FUNC_EXT
	#define DMS_FUNC_VOL	volatile
#else
	#define DMS_FUNC_EXT	extern
	#define DMS_FUNC_VOL	extern
#endif	///	__C_DMS_FUNCTIONS_

#define DMS_DDS_CTRL_TIM_CH	TIM_CHANNEL_3

/****************************************************************************
 *							VARIABLES										*
 ****************************************************************************/
DMS_FUNC_VOL long		 	dms_sweep_timer_cnt;	//<	Счетчик.	

DMS_FUNC_EXT tsDeviceCfg 	DeviceCfg;				//<	Настройка устройства.
DMS_FUNC_EXT tsDeviceInfo	DeviceInfo;				//<	Информация об устройстве.

DMS_FUNC_EXT tsActionTimer	DdsTimer;

/****************************************************************************
 *						FUNCTIONS DECLARATIONS								*
 ****************************************************************************/
int  	dms_init 			( void );
void 	dms_thread_func 	( void );
uint8_t dms_recv_bulk_data	( uint8_t *Buf, uint16_t *Len );

#endif	///	__H_DMS_FUNCTIONS_

/************************** END OF FILE *************************************/
