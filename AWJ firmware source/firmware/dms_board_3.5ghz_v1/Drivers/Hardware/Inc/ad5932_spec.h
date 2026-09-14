/***********************************************************************
 *	@file:		ad5932.h
 *	@brief:		Заголовочный файл библиотеки для работы с AD5932.
 *	@version:	v1.0
 *	@author:	Kirillov A.V.
 *	@date:		16/12/2019
 ***********************************************************************/
#ifndef __H_AD5932_SPECIFICATION_
#define __H_AD5932_SPECIFICATION_

/***********************************************************************
 *			REGISTERS
 ***********************************************************************/
#define AD5932_REGISTER0		0
#define AD5932_REGISTER1		1
#define AD5932_REGISTER2		2
#define AD5932_REGISTER3		3
#define AD5932_REGISTER4		4
#define AD5932_REGISTER6		6
#define AD5932_REGISTER12		12
#define AD5932_REGISTER13		13

#define AD5932_REGISTERS_SIZE	7

/***********************************************************************
 *			REGISTER 0 - Control bits
 ***********************************************************************/
#define AD5932_B24_REG			AD5932_REGISTER0
#define AD5932_B24_BITP			11
#define AD5932_B24_MASK			1
#define AD5932_B24_BITM			(AD5932_B24_MASK << AD5932_B24_BITP)

#define AD5932_DAC_EN_REG		AD5932_REGISTER0
#define AD5932_DAC_EN_BITP		10
#define AD5932_DAC_EN_MASK		1
#define AD5932_DAC_EN_BITM		(AD5932_DAC_EN_MASK << AD5932_DAC_EN_BITP)

#define AD5932_SINE_TRI_REG		AD5932_REGISTER0
#define AD5932_SINE_TRI_BITP	9
#define AD5932_SINE_TRI_MASK	1
#define AD5932_SINE_TRI_BITM	(AD5932_SINE_TRI_MASK << AD5932_SINE_TRI_BITP)

#define AD5932_MSBOUTEN_REG		AD5932_REGISTER0
#define AD5932_MSBOUTEN_BITP	8
#define AD5932_MSBOUTEN_MASK	1
#define AD5932_MSBOUTEN_BITM	(AD5932_MSBOUTEN_MASK << AD5932_MSBOUTEN_BITP)

#define AD5932_INTEXT_INC_REG	AD5932_REGISTER0
#define AD5932_INTEXT_INC_BITP	5
#define AD5932_INTEXT_INC_MASK	1
#define AD5932_INTEXT_INC_BITM	(AD5932_INTEXT_INC_MASK << AD5932_INTEXT_INC_BITP)

#define AD5932_SYNCSEL_REG		AD5932_REGISTER0
#define AD5932_SYNCSEL_BITP		3
#define AD5932_SYNCSEL_MASK		1
#define AD5932_SYNCSEL_BITM		(AD5932_SYNCSEL_MASK << AD5932_SYNCSEL_BITP)

#define AD5932_SYNCOUT_REG		AD5932_REGISTER0
#define AD5932_SYNCOUT_BITP		2
#define AD5932_SYNCOUT_MASK		1
#define AD5932_SYNCOUT_BITM		(AD5932_SYNCOUT_MASK << AD5932_SYNCOUT_BITP)

/***********************************************************************
 *			REGISTER 1 - Number of increments
 ***********************************************************************/
#define AD5932_N_INCR_REG				AD5932_REGISTER1
#define AD5932_N_INCR_BITP				0
#define AD5932_N_INCR_MASK				0xFFFF
#define AD5932_N_INCR_BITM				(AD5932_N_INCR_MASK << AD5932_N_INCR_BITP)

/***********************************************************************
 *			REGISTER 2 - Lower 12 bits of delta frequency
 ***********************************************************************/
#define AD5932_LOW_DELTA_FREQ_REG		AD5932_REGISTER2
#define AD5932_LOW_DELTA_FREQ_BITP		0
#define AD5932_LOW_DELTA_FREQ_MASK		0xFFFF
#define AD5932_LOW_DELTA_FREQ_BITM		(AD5932_LOW_DELTA_FREQ_MASK << AD5932_LOW_DELTA_FREQ_BITP)

/***********************************************************************
 *			REGISTER 3 - High 12 bits of delta frequency
 ***********************************************************************/
#define AD5932_HIGH_DELTA_FREQ_REG		AD5932_REGISTER3
#define AD5932_HIGH_DELTA_FREQ_BITP		0
#define AD5932_HIGH_DELTA_FREQ_MASK		0xFFFF
#define AD5932_HIGH_DELTA_FREQ_BITM		(AD5932_HIGH_DELTA_FREQ_MASK << AD5932_HIGH_DELTA_FREQ_BITP)

/***********************************************************************
 *			REGISTER 4 - Increment interval
 ***********************************************************************/
#define AD5932_INC_INT_REG				AD5932_REGISTER4
#define AD5932_INC_INT_BITP				0
#define AD5932_INC_INT_MASK				0xFFFF
#define AD5932_INC_INT_BITM				(AD5932_INC_INT_MASK << AD5932_INC_INT_BITP)

/***********************************************************************
 *			REGISTER 12 - Low start frequency
 ***********************************************************************/
#define AD5932_LOW_STRT_FREQ_REG		AD5932_REGISTER12
#define AD5932_LOW_STRT_FREQ_BITP		0
#define AD5932_LOW_STRT_FREQ_MASK		0xFFFF
#define AD5932_LOW_STRT_FREQ_BITM		(AD5932_LOW_STRT_FREQ_MASK << AD5932_LOW_STRT_FREQ_BITP)

/***********************************************************************
  *			REGISTER 13 - High start frequency
 ***********************************************************************/
#define AD5932_HIGH_STRT_FREQ_REG		AD5932_REGISTER13
#define AD5932_HIGH_STRT_FREQ_BITP		0
#define AD5932_HIGH_STRT_FREQ_MASK		0xFFFF
#define AD5932_HIGH_STRT_FREQ_BITM		(AD5932_HIGH_STRT_FREQ_MASK << AD5932_HIGH_STRT_FREQ_BITP)

#endif	//	__H_AD5932_SPECIFICATION_

/********************* END OF FILE ********************************/
