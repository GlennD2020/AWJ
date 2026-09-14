/****************************************************************************
 * @file:	ad9106_spec.h
 * @date:	15/12/2020
 * @author: Kirillov A.V.
 ***************************************************************************/
#ifndef __H_AD9106_SPECIFICATION_
#define __H_AD9106_SPECIFICATION_

//	Для отключения сообщения о неизвестных "аттрибутов" pragma
#pragma GCC diagnostic ignored "-Wunknown-pragmas"

//***********************************************************************
#pragma region "REGISTER ADDRESS"

#define AD9106_REG0	    0
#define AD9106_REG1	    1
#define AD9106_REG2	    2
#define AD9106_REG3	    3
#define AD9106_REG4	    4
#define AD9106_REG5	    5
#define AD9106_REG6	    6
#define AD9106_REG7	    7
#define AD9106_REG8	    8
#define AD9106_REG9	    9
#define AD9106_REG10	10
#define AD9106_REG11	11
#define AD9106_REG12	12
#define AD9106_REG13	13
#define AD9106_REG14	14
#define AD9106_REG15	15
#define AD9106_REG16	16
#define AD9106_REG17	17
#define AD9106_REG18	18
#define AD9106_REG19	19
#define AD9106_REG20	20
#define AD9106_REG21	21
#define AD9106_REG22	22
#define AD9106_REG23	23
#define AD9106_REG24	24
#define AD9106_REG25	25
#define AD9106_REG26	26
#define AD9106_REG27	27
#define AD9106_REG28	28
#define AD9106_REG29	29
#define AD9106_REG30	30
#define AD9106_REG31	31
#define AD9106_REG32	32
#define AD9106_REG33	33
#define AD9106_REG34	34
#define AD9106_REG35	35
#define AD9106_REG36	36
#define AD9106_REG37	37
#define AD9106_REG38	38
#define AD9106_REG39	39
#define AD9106_REG40	40
#define AD9106_REG41	41
#define AD9106_REG42	42
#define AD9106_REG43	43
#define AD9106_REG44	44
#define AD9106_REG45	45
#define AD9106_REG46	46
#define AD9106_REG47	47
#define AD9106_REG48	48
#define AD9106_REG49	49
#define AD9106_REG50	50
#define AD9106_REG51	51
#define AD9106_REG52	52
#define AD9106_REG53	53
#define AD9106_REG54	54
#define AD9106_REG55	55
#define AD9106_REG56	56
#define AD9106_REG57	57
#define AD9106_REG58	58
#define AD9106_REG59	59
#define AD9106_REG60	60
#define AD9106_REG61	61
#define AD9106_REG62	62
#define AD9106_REG63	63
#define AD9106_REG64	64
#define AD9106_REG65	65
#define AD9106_REG66	66
#define AD9106_REG67	67
#define AD9106_REG68	68
#define AD9106_REG69	69
#define AD9106_REG70	70
#define AD9106_REG71	71
#define AD9106_REG72	72
#define AD9106_REG73	73
#define AD9106_REG74	74
#define AD9106_REG75	75
#define AD9106_REG76	76
#define AD9106_REG77	77
#define AD9106_REG78	78
#define AD9106_REG79	79
#define AD9106_REG80	80
#define AD9106_REG81	81
#define AD9106_REG82	82
#define AD9106_REG83	83
#define AD9106_REG84	84
#define AD9106_REG85	85
#define AD9106_REG86	86
#define AD9106_REG87	87
#define AD9106_REG88	88
#define AD9106_REG89	89
#define AD9106_REG90	90
#define AD9106_REG91	91
#define AD9106_REG92	92
#define AD9106_REG93	93
#define AD9106_REG94	94
#define AD9106_REG95	95
#define AD9106_REG96	96

#pragma endregion

//************************************************************************
#pragma region "REGION 0"
//	SPI Control Register (SPICONFIG, Address 0x00)
/*  LSBFIRSTM - LSB first selection. */
#define AD9106_LSBFIRSTM_REG	    AD9106_REGR0
#define AD9106_LSBFIRSTM_BITP	    0
#define AD9106_LSBFIRSTM_MASK	    1
#define AD9106_LSBFIRSTM_BITM	    (AD9106_LSBFIRSTM_MASK << AD9106_LSBFIRSTM_BITP)
/*  SPI3WIREM - Selects if SPI is using 3-wire or 4-wire interface. */
#define AD9106_SPI3WIREM_REG		AD9106_REGR0
#define AD9106_SPI3WIREM_BITP       1
#define AD9106_SPI3WIREM_MASK		1
#define AD9106_SPI3WIREM_BITM       (AD9106_SPI3WIREM_MASK << AD9106_SPI3WIREM_BITP)
/*  RESETM - Executes software reset of SPI and controllers, reloads default register
values, except for Register 0x00.   */
#define AD9106_RSTM_REG		        AD9106_REGR0
#define AD9106_RSTM_BITP		    2
#define AD9106_RSTM_MASK		    1
#define AD9106_RSTM_BITM		    (AD9106_RSTM_MASK << AD9106_RSTM_BITP)
/*  DOUBLESPIM - Double SPI data line.  */
#define AD9106_DOUBLESPIM_REG		AD9106_REGR0
#define AD9106_DOUBLESPIM_BITP		3
#define AD9106_DOUBLESPIM_MASK		1
#define AD9106_DOUBLESPIM_BITM      (AD9106_DOUBLESPIM_MASK << AD9106_DOUBLESPIM_BITP)
/*  SPI_DRVM - Double drive ability for SPI output. */
#define AD9106_SPI_DRVM_REG	        AD9106_REGR0
#define AD9106_SPI_DRVM_BITP	    4
#define AD9106_SPI_DRVM_MASK	    1
#define AD9106_SPI_DRVM_BITM	    (AD9106_SPI_DRVM_MASK << AD9106_SPI_DRVM_BITP)
/*  DOUT_ENM - Enable DOUT signal on SDO/SDI2/DOUT pin. */
#define AD9106_DOUT_ENM_REG	        AD9106_REGR0
#define AD9106_DOUT_ENM_BITP	    5
#define AD9106_DOUT_ENM_MASK	    1
#define AD9106_DOUT_ENM_BITM	    (AD9106_DOUT_ENM_MASK << AD9106_DOUT_ENM_BITP)
/*  DOUT_EN - Enable DOUT signal on SDO/SDI2/DOUT pin. 
    0 - SDO/SDI2 function input/output.
    1 - DOUT function output.   */
#define AD9106_DOUT_EN_REG	        AD9106_REGR0
#define AD9106_DOUT_EN_BITP	        10
#define AD9106_DOUT_EN_MASK	        1
#define AD9106_DOUT_EN_BITM	        (AD9106_DOUT_EN_MASK << AD9106_DOUT_EN_BITP)
/*  SPI_DRV - Double drive ability for SPI output. 
    0 - Single SPI output drive ability.
    1 - Two-time drive ability on SPI output.   */
#define AD9106_SPI_DRV_REG	        AD9106_REGR0
#define AD9106_SPI_DRV_BITP	        11
#define AD9106_SPI_DRV_MASK	        1
#define AD9106_SPI_DRV_BITM	        (AD9106_SPI_DRV_MASK << AD9106_SPI_DRV_BITP)
/*  DOUBLESPI - Double SPI data line. 
    0 - The SPI port has only one data line and can be used as a 3-wire or 4-wire interface.
    1 - The SPI port has two data lines: both bidirectional defining a pseudo dual 3-wire 
    interface where CS and SCLK are shared between the two ports.
    This mode is only available for RAM data read or write. */
#define AD9106_DOUBLESPI_REG        AD9106_REGR0
#define AD9106_DOUBLESPI_BITP       12
#define AD9106_DOUBLESPI_MASK	    1
#define AD9106_DOUBLESPI_BITM	    (AD9106_DOUBLESPI_MASK << AD9106_DOUBLESPI_BITP)
/*  RESET - Executes software reset of SPI and controllers, reloads default register
values, except for Register 0x00. 
    0 - Normal status.
    1 - Resets whole register map, except for Register 0x00.    */
#define AD9106_RST_REG			    AD9106_REGR0
#define AD9106_RST_BITP			    13
#define AD9106_RST_MASK			    1
#define AD9106_RST_BITM			    (AD9106_RST_MASK << AD9106_RST_BITP)
/*  SPI3WIRE - Selects if SPI is using 3-wire or 4-wire interface. 
    0 4-wire SPI.
    1 3-wire SPI.   */
#define AD9106_SPI3WIRE_REG			AD9106_REGR0
#define AD9106_SPI3WIRE_BITP		14
#define AD9106_SPI3WIRE_MASK		1
#define AD9106_SPI3WIRE_BITM        (AD9106_SPI3WIRE_MASK << AD9106_SPI3WIRE_BITP)
/*  LSBFIRST - LSB first selection. 
    0 MSB first per SPI standard (default).
    1 LSB first per SPI standard.   */
#define AD9106_LSBFIRST_REG			AD9106_REGR0
#define AD9106_LSBFIRST_BITP		15
#define AD9106_LSBFIRST_MASK		1
#define AD9106_LSBFIRST_BITM        (AD9106_LSBFIRST_MASK << AD9106_LSBFIRST_BITP)

#pragma endregion "REGION 0"

//************************************************************************
#pragma region "REGION 1"

#pragma endregion "REGION 1"

//***********************************************************************
#pragma region "REGION 69"
//	REGISTER 69 (0x45) - Pattern Control 2 Register (DDSx_CONFIG, Address 0x45)

/*  TW_MEM_EN - Enable DDS tuning word input coming from RAM reading using
START_ADDR1. Because tuning word is 24 bits and RAM data is 12 bits, 
12 bits are set to 0s depending on the value of the TW_MEM_SHIFT bits in
the TW_RAM_CONFIG register. Default is coming from the SPI map, DDSTW. */
#define AD9106_TW_MEM_EN_REG	    AD9106_REGR69
#define AD9106_TW_MEM_EN_BITP	    0
#define AD9106_TW_MEM_EN_MASK	    1
#define AD9106_TW_MEM_EN_BITM	    (AD9106_TW_MEM_EN_MASK << AD9106_TW_MEM_EN_BITP)

/*  DDS_MSB_EN1 - Enable the clock for the RAM address. Increment is coming from 
the DDS1 MSB. Default is coming from DAC clock. */
#define AD9106_DDS_MSB_EN1_REG	    AD9106_REGR69
#define AD9106_DDS_MSB_EN1_BITP	    2
#define AD9106_DDS_MSB_EN1_MASK	    1
#define AD9106_DDS_MSB_EN1_BITM	    (AD9106_DDS_MSB_EN1_MASK << AD9106_DDS_MSB_EN1_BITP)

/* DDS_COS_EN1 - Enable DDS1 cosine output of DDS instead of sine wave. */
#define AD9106_DDS_COS_EN1_REG	    AD9106_REGR69
#define AD9106_DDS_COS_EN1_BITP	    3
#define AD9106_DDS_COS_EN1_MASK	    1
#define AD9106_DDS_COS_EN1_BITM	    (AD9106_DDS_COS_EN1_MASK << AD9106_DDS_COS_EN1_BITP)

/*  DDS_MSB_EN2 - Enable the clock for the RAM address. Increment is coming from 
the DDS2 MSB. Default is coming from DAC clock. */
#define AD9106_DDS_MSB_EN2_REG	    AD9106_REGR69
#define AD9106_DDS_MSB_EN2_BITP	    6
#define AD9106_DDS_MSB_EN2_MASK	    1
#define AD9106_DDS_MSB_EN2_BITM	    (AD9106_DDS_MSB_EN2_MASK << AD9106_DDS_MSB_EN2_BITP)

/* DDS_COS_EN2 - Enable DDS2 cosine output of DDS instead of sine wave. */
#define AD9106_DDS_COS_EN2_REG	    AD9106_REGR69
#define AD9106_DDS_COS_EN2_BITP	    7
#define AD9106_DDS_COS_EN2_MASK	    1
#define AD9106_DDS_COS_EN2_BITM	    (AD9106_DDS_COS_EN2_MASK << AD9106_DDS_COS_EN2_BITP)

/*  PHASE_MEM_EN3 - Enable DDS3 phase offset input coming from RAM reading START_ADDR3.
Because phase word is 8 bits and RAM data is 12 bits, only 8 MSB of RAM are taken into account. 
Default is coming from SPI map, DDS3_PHASE. */
#define AD9106_PHASE_MEM_EN3_REG	AD9106_REGR69
#define AD9106_PHASE_MEM_EN3_BITP	9
#define AD9106_PHASE_MEM_EN3_MASK	1
#define AD9106_PHASE_MEM_EN3_BITM	(AD9106_PHASE_MEM_EN3_MASK << AD9106_PHASE_MEM_EN3_BITP)

/*  DDS_MSB_EN3 - Enable the clock for the RAM address. Increment is coming from 
the DDS3 MSB. Default is coming from DAC clock. */
#define AD9106_DDS_MSB_EN3_REG	    AD9106_REGR69
#define AD9106_DDS_MSB_EN3_BITP	    10
#define AD9106_DDS_MSB_EN3_MASK	    1
#define AD9106_DDS_MSB_EN3_BITM	    (AD9106_DDS_MSB_EN3_MASK << AD9106_DDS_MSB_EN3_BITP)

/* DDS_COS_EN3 - Enable DDS3 cosine output of DDS instead of sine wave. */
#define AD9106_DDS_COS_EN3_REG	    AD9106_REGR69
#define AD9106_DDS_COS_EN3_BITP	    11
#define AD9106_DDS_COS_EN3_MASK	    1
#define AD9106_DDS_COS_EN3_BITM	    (AD9106_DDS_COS_EN3_MASK << AD9106_DDS_COS_EN3_BITP)

/*  DDS_MSB_EN4 - Enable the clock for the RAM address. Increment is coming from 
the DDS4 MSB. Default is coming from DAC clock. */
#define AD9106_DDS_MSB_EN4_REG	    AD9106_REGR69
#define AD9106_DDS_MSB_EN4_BITP	    14
#define AD9106_DDS_MSB_EN4_MASK	    1
#define AD9106_DDS_MSB_EN4_BITM	    (AD9106_DDS_MSB_EN4_MASK << AD9106_DDS_MSB_EN4_BITP)

/* DDS_COS_EN4 - Enable DDS4 cosine output of DDS instead of sine wave. */
#define AD9106_DDS_COS_EN4_REG	    AD9106_REGR69
#define AD9106_DDS_COS_EN4_BITP	    15
#define AD9106_DDS_COS_EN4_MASK	    1
#define AD9106_DDS_COS_EN4_BITM	    (AD9106_DDS_COS_EN4_MASK << AD9106_DDS_COS_EN4_BITP)

#pragma endregion "REGION 69"

#endif /// __H_AD9106_SPECIFICATION_

/************************** END OF FILE *************************************/
