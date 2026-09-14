/**
 *	@file:		max2871_spec.h
 *	@remark:	Defines for works with MAX2871.
 *	@author:	Alexandr Kirillov
 */
#ifndef __H_SPECIFICATION_MAX2871_
#define __H_SPECIFICATION_MAX2871_

/***********************************************************************
 *		DEFINES
 ***********************************************************************/
#define MAX2871_REF_FREQ		50000000

#define MAX2871_VCO_FREQ_MIN	3e9
#define MAX2871_VCO_FREQ_MAX	6e9

#define MAX2871_OUT_FREQ_MIN	23.5e6
#define MAX2871_OUT_FREQ_MAX	6e9

/***********************************************************************
 *		REGISTERS ADDRESS
 ***********************************************************************/
#define MAX2871_REG0			0
#define MAX2871_REG1			1
#define MAX2871_REG2			2
#define MAX2871_REG3			3
#define MAX2871_REG4			4
#define MAX2871_REG5			5

#define MAX2871_MASK_ADDR		0x00000007

/***********************************************************************
 *		REGISTER 0
 ***********************************************************************/
//{
/**
 *	@brief	Int-N or Frac-N Mode Control
 *	@remark	0 = Enables the fractional-N mode
 *			1 = Enables the integer-N mode
 *			The LDF bit must also be set to the appropriate mode.
 */
#define MAX2871_REG_INT			MAX2871_REG0
#define MAX2871_BITP_INT		31
#define MAX2871_MASK_INT		0x00000001
#define MAX2871_BITM_INT		(MAX2871_MASK_INT<<MAX2871_BITP_INT)
/**
 *	@brief 	Integer Division Value
 *	@remark	Sets integer part (N-divider) of the feedback divider factor. 
 *			All integer	values from 16 to 65,535 are allowed for integer mode. 
 *			Integer values from 0 to 15 are not allowed. Integer values 
 *			from 19 to 4091 are allowed for fractional mode.
 */
#define MAX2871_REG_N			MAX2871_REG0
#define MAX2871_BITP_N			15
#define MAX2871_MASK_N			0x0000FFFF
#define MAX2871_BITM_N			(MAX2871_MASK_N<<MAX2871_BITP_N)
/**
 *	@brief	Fractional Division Value
 *	@remark	Sets fractional value:
 *			000000000000 = 0 (see F0I bit description)
 *			000000000001 = 1
 *			----
 *			111111111110 = 4094
 * 			111111111111 = 4095
 */
#define MAX2871_REG_FRAC		MAX2871_REG0
#define MAX2871_BITP_FRAC		3
#define MAX2871_MASK_FRAC		0x00000FFF
#define MAX2871_BITM_FRAC		(MAX2871_MASK_FRAC<<MAX2871_BITP_FRAC)
//}
/***********************************************************************
 *		REGISTER 1
 ***********************************************************************/
//{
/**
 *	@brief	CP Linearity
 *	@remark Sets CP linearity mode.
 *			00 = Disables the CP linearity mode (integer-N mode)
 *			01 = CP linearity 10% mode (frac-N mode)
 *			10 = CP linearity 20% mode (frac-N mode)
 *			11 = CP linearity 30% mode (frac-N mode)
 */
#define MAX2871_REG_CPL			MAX2871_REG1
#define MAX2871_BITP_CPL		29
#define MAX2871_MASK_CPL		0x00000003
#define MAX2871_BITM_CPL		(MAX2871_MASK_CPL<<MAX2871_BITP_CPL)
/**
 *	@brief	Charge Pump Test
 *	@remark	Sets charge-pump test modes.
 *			00 = Normal mode
 *			01 = Long Reset mode
 *			10 = Force CP into source mode
 *			11 = Force CP into sink mode
 */	
#define MAX2871_REG_CPT			MAX2871_REG1
#define MAX2871_BITP_CPT		27
#define MAX2871_MASK_CPT		0x00000003
#define MAX2871_BITM_CPT		(MAX2871_MASK_CPT<<MAX2871_BITP_CPT)
/**
 *	@brief	Phase Value
 *	@remark	Sets phase value. See the Phase Adjustment section.
 *			000000000000 = 0
 *			000000000001 = 1 (recommended)
 *			-----
 *			111111111111 = 4095
 */	
#define MAX2871_REG_P			MAX2871_REG1
#define MAX2871_BITP_P			15
#define MAX2871_MASK_P			0x00000FFF
#define MAX2871_BITM_P			(MAX2871_MASK_P<<MAX2871_BITP_P)
/**
 *	@brief	Modulus Value(M)
 *	@remark	Fractional modulus value used to program fVCO. See the Int, 
 *			Frac, Mod and R Counter Relationship section. 
 *			Double buffered by register 0.
 *			000000000000 = Not Valid
 *			000000000001 = Not Valid
 *			000000000010 = 2
 *			-----
 *			111111111111 = 4095
 */	
#define MAX2871_REG_M			MAX2871_REG1
#define MAX2871_BITP_M			3
#define MAX2871_MASK_M			0x00000FFF
#define MAX2871_BITM_M			(MAX2871_MASK_M<<MAX2871_BITP_M)
//}
/***********************************************************************
 *		REGISTER 2
 ***********************************************************************/
//{
/**
 *	@brief	Lock-Detect Speed
 *	@remark	Lock-detect speed adjustment.
 *			0 = fPFD ≤ 32MHz.
 *			1 = fPFD > 32MHz.
 */	
#define MAX2871_REG_LDS			MAX2871_REG2
#define MAX2871_BITP_LDS		31
#define MAX2871_MASK_LDS		0x00000001
#define MAX2871_BITM_LDS		(MAX2871_MASK_LDS<<MAX2871_BITP_LDS)
/**
 *	@brief	Frac-N Sigma Delta Noise Mode
 *	@remark	Sets noise mode (see the Low-Spur Mode section.)
 *			00 = Low-noise mode
 *			01 = Reserved
 *			10 = Low-spur mode 1
 *			11 = Low-spur mode 2
 */		
#define MAX2871_REG_SDN			MAX2871_REG2
#define MAX2871_BITP_SDN		29
#define MAX2871_MASK_SDN		0x00000003
#define MAX2871_BITM_SDN		(MAX2871_MASK_SDN<<MAX2871_BITP_SDN)
/**
 *	@brief	MUX Confguration.
 *	@remark	Sets MUX pin confguration (MSB bit located register 05).
 *			0000 = Three-state output
 *			0001 = D_VDD
 *			0010 = D_GND
 *			0011 = R-divider output
 *			0100 = N-divider output/2
 *			0101 = Analog lock detect
 * 			0110 = Digital lock detect
 *			0111 = Sync Input
 *			1000 : 1011 = Reserved
 *			1100 = Read SPI registers 06
 *			1101 : 1111= Reserved
 */	
#define MAX2871_REG_MUX			MAX2871_REG2
#define MAX2871_BITP_MUX		26
#define MAX2871_MASK_MUX		0x00000003
#define MAX2871_BITM_MUX		(MAX2871_MASK_MUX<<MAX2871_BITP_MUX)
/**
 *	@brief	Reference Doubler Mode
 *	@remark	Sets reference doubler mode.
 *			0 = Disable reference doubler
 *			1 = Enable reference doubler
 */		
#define MAX2871_REG_DBR			MAX2871_REG2
#define MAX2871_BITP_DBR		25
#define MAX2871_MASK_DBR		0x00000001
#define MAX2871_BITM_DBR		(MAX2871_MASK_DBR<<MAX2871_BITP_DBR)
/**
 *	@brief	Reference Div2 Mode
 *	@remark	Sets reference divide-by-2 mode.
 *			0 = Disable reference divide-by-2.
 *			1 = Enable reference divide-by-2.
 */		
#define MAX2871_REG_RDIV2		MAX2871_REG2
#define MAX2871_BITP_RDIV2		24
#define MAX2871_MASK_RDIV2		0x00000001
#define MAX2871_BITM_RDIV2		(MAX2871_MASK_RDIV2<<MAX2871_BITP_RDIV2)
/**
 *	@brief	Reference Divider Mode
 *	@remark Sets reference divide value (R). 
 *			Double buffered by register 0.
 *			0000000000 = 0 (unused)
 *			0000000001 = 1
 *			-----
 *			1111111111 = 1023
 */		
#define MAX2871_REG_R			MAX2871_REG2
#define MAX2871_BITP_R			14
#define MAX2871_MASK_R			0x000003FF
#define MAX2871_BITM_R			(MAX2871_MASK_R<<MAX2871_BITP_R)
/**
 *	@brief	Double Buffer
 *	@remark	Sets double buffer mode.
 *			0 = Disabled
 *			1 = Enabled
 */		
#define MAX2871_REG_REG4DB		MAX2871_REG2
#define MAX2871_BITP_REG4DB		13
#define MAX2871_MASK_REG4DB		0x00000001
#define MAX2871_BITM_REG4DB		(MAX2871_MASK_REG4DB<<MAX2871_BITP_REG4DB)
/**
 *	@brief	Charge-Pump Current
 *	@remark	Sets charge-pump current in mA (RSET = 5.1kΩ). 
 *			Double buffered by register 0.
 *			ICP = 1.63/RSET × (1+CP[3:0])
 */		
#define MAX2871_REG_CP			MAX2871_REG2
#define MAX2871_BITP_CP			9
#define MAX2871_MASK_CP			0x0000000F
#define MAX2871_BITM_CP			(MAX2871_MASK_CP<<MAX2871_BITP_CP)
/**
 *	@brief	Lock-Detect Function
 *	@remark	Sets lock-detect function.
 *			0 = Frac-N lock detect.
 *			1 = Int-N lock detect.
 */	
#define MAX2871_REG_LDF			MAX2871_REG2
#define MAX2871_BITP_LDF		8
#define MAX2871_MASK_LDF		0x00000001
#define MAX2871_BITM_LDF		(MAX2871_MASK_LDF<<MAX2871_BITP_LDF)
/**
 *	@brief	Lock-Detect Precision
 *	@remark	Sets lock-detect precision.
 *			0 = 10ns
 *			1 = 6ns
 */		
#define MAX2871_REG_LDP			MAX2871_REG2
#define MAX2871_BITP_LDP		7
#define MAX2871_MASK_LDP		0x00000001
#define MAX2871_BITM_LDP		(MAX2871_MASK_LDP<<MAX2871_BITP_LDP)
/**
 *	@brief	Phase Detector Polarity
 *	@remark	Sets phase detector polarity.
 *			0 = Negative
 *			1 = Positive (default)
 */	
#define MAX2871_REG_PDP			MAX2871_REG2
#define MAX2871_BITP_PDP		6
#define MAX2871_MASK_PDP		0x00000001
#define MAX2871_BITM_PDP		(MAX2871_MASK_PDP<<MAX2871_BITP_PDP)
/**
 *	@brief	Shutdown Mode
 *	@remark	Sets power-down mode.
 *			0 = Normal mode.
 *			1 = Device shutdown.
 */	
#define MAX2871_REG_SHDN		MAX2871_REG2
#define MAX2871_BITP_SHDN		5
#define MAX2871_MASK_SHDN		0x00000001
#define MAX2871_BITM_SHDN		(MAX2871_MASK_SHDN<<MAX2871_BITP_SHDN)
#define MAX2871_SHDN			1
/**
 *	@brief	Charge Pump Output HighImpedance Mode
 *	@remark Sets charge-pump output high-impedance mode.
 *			0 = Disabled
 *			1 = Enabled
 */	
#define MAX2871_REG_TRI			MAX2871_REG2
#define MAX2871_BITP_TRI		4
#define MAX2871_MASK_TRI		0x00000001
#define MAX2871_BITM_TRI		(MAX2871_MASK_TRI<<MAX2871_BITP_TRI)
/**
 *	@brief	Counter Reset
 *	@remark	Sets counter reset mode.
 *			0 = Normal operation.
 *			1 = R and N counters reset.
 */	
#define MAX2871_REG_RST			MAX2871_REG2
#define MAX2871_BITP_RST		3
#define MAX2871_MASK_RST		0x00000001
#define MAX2871_BITM_RST		(MAX2871_MASK_RST<<MAX2871_BITP_RST)
//}
/***********************************************************************
 *		REGISTER 3
 ***********************************************************************/
//{
/**
 *	@brief	VCO
 *	@remark	Manual selection of VCO and VCO sub-band when VAS is disabled.
 *			000000 = VCO0
 *			….
 *			111111 = VCO63
 */	
#define MAX2871_REG_VCO			MAX2871_REG3
#define MAX2871_BITP_VCO		26
#define MAX2871_MASK_VCO		0x0000003F
#define MAX2871_BITM_VCO		(MAX2871_MASK_VCO<<MAX2871_BITP_VCO)
/**
 *	@brief	VAS_SHDN
 *	@remark	Sets VAS shutdown mode.
 *			0 = VAS enabled
 *			1 = VAS disabled
 */	
#define MAX2871_REG_VAS_SHDN	MAX2871_REG3
#define MAX2871_BITP_VAS_SHDN	25
#define MAX2871_MASK_VAS_SHDN	0x00000001
#define MAX2871_BITM_VAS_SHDN	(MAX2871_MASK_VAS_SHDN<<MAX2871_BITP_VAS_SHDN)
/**
 *	@brief	VAS_TEMP
 *	@remark	Sets VAS response to temperature drift.
 *			0 = VAS temperature compensation disabled.
 *			1 = VAS temperature compensation enabled.
 */	
#define MAX2871_REG_VAS_TEMP	MAX2871_REG3
#define MAX2871_BITP_VAS_TEMP	24
#define MAX2871_MASK_VAS_TEMP	0x00000001
#define MAX2871_BITM_VAS_TEMP	(MAX2871_MASK_VAS_TEMP<<MAX2871_BITP_VAS_TEMP)
/**
 *	@brief	Cycle Slip Mode
 *	@remark	Cycle Slip Mode
 *			0 = Disable Cycle Slip Reduction
 *			1 = Enable Cycle Slip Reduction
 */	
#define MAX2871_REG_CSM			MAX2871_REG3
#define MAX2871_BITP_CSM		18
#define MAX2871_MASK_CSM		0x00000001
#define MAX2871_BITM_CSM		(MAX2871_MASK_CSM<<MAX2871_BITP_CSM)
/**
 *	@brief	Mute Delay Mode
 *	@remark	Mute Delay
 *			0 = Do not delay LD to MTLD function to prevent ﬂickering.
 *			1= Delay LD to MTLD function to prevent ﬂickering.
 */	
#define MAX2871_REG_MUTEDEL		MAX2871_REG3
#define MAX2871_BITP_MUTEDEL	17
#define MAX2871_MASK_MUTEDEL	0x00000001
#define MAX2871_BITM_MUTEDEL	(MAX2871_MASK_MUTEDEL<<MAX2871_BITP_MUTEDEL)
/**
 *	@brief	Clock Divider Mode
 *	@remark	Sets clock divider mode.
 *			00 = Mute until Lock Delay
 *			01 = Fast-lock enabled
 *			10 = Phase Adjustment mode
 *			11 = Reserved
 */	
#define MAX2871_REG_CDM			MAX2871_REG3
#define MAX2871_BITP_CDM		15
#define MAX2871_MASK_CDM		0x00000003
#define MAX2871_BITM_CDM		(MAX2871_MASK_CDM<<MAX2871_BITP_CDM)
/**
 *	@brief	Clock Divider Value
 *	@remark	Sets 12-bit clock divider value.
 *			000000000000 = Unused
 *			000000000001 = 1
 *			000000000010 = 2
 *			-----
 *			111111111111 = 4095
 */	
#define MAX2871_REG_CDIV		MAX2871_REG3
#define MAX2871_BITP_CDIV		3
#define MAX2871_MASK_CDIV		0x00000FFF
#define MAX2871_BITM_CDIV		(MAX2871_MASK_CDIV<<MAX2871_BITP_CDIV)
//}
/***********************************************************************
 *		REGISTER 4
 ***********************************************************************/
//{
/**
 *	@brief	Shutdown VCO LDO
 *	@remark	Sets Shutdown VCO LDO mode.
 *			0 = Enables LDO.
 *			1 = Disables LDO.
 */	
#define MAX2871_REG_SDLDO		MAX2871_REG4
#define MAX2871_BITP_SDLDO		28
#define MAX2871_MASK_SDLDO		0x00000001
#define MAX2871_BITM_SDLDO		(MAX2871_MASK_SDLDO<<MAX2871_BITP_SDLDO)
/**
 *	@brief	Shutdown VCO Divider
 *	@remark	Sets Shutdown VCO Divider mode.
 *			0 = Enables VCO Divider
 *			1 = Disables VCO Divider
 */	
#define MAX2871_REG_SDDIV		MAX2871_REG4
#define MAX2871_BITP_SDDIV		27
#define MAX2871_MASK_SDDIV		0x00000001
#define MAX2871_BITM_SDDIV		(MAX2871_MASK_SDDIV<<MAX2871_BITP_SDDIV)
/**
 *	@brief	Shutdown Reference Input
 *	@remark	Sets Shutdown Reference input mode.
 *			0 = Enables Reference Input.
 *			1 = Disables Reference Input.
 */	
#define MAX2871_REG_SDREF		MAX2871_REG4
#define MAX2871_BITP_SDREF		26
#define MAX2871_MASK_SDREF		0x00000001
#define MAX2871_BITM_SDREF		(MAX2871_MASK_SDREF<<MAX2871_BITP_SDREF)
/**
 *	@brief	Band-Select MSBs
 *	@remark	Sets Band-Select clock divider MSBs. See bits[19:12].
 */	
#define MAX2871_REG_BS_H		MAX2871_REG4
#define MAX2871_BITP_BS_H		24
#define MAX2871_MASK_BS_H		0x00000003
#define MAX2871_BITM_BS_H		(MAX2871_MASK_BS_H<<MAX2871_BITP_BS_H)
/**
 *	@brief	VCO Feedback Mode.
 *	@remark	Sets VCO to N counter feedback mode.
 *			0 = Divided.
 *			1 = Fundamental.
 */	
#define MAX2871_REG_FB			MAX2871_REG4
#define MAX2871_BITP_FB			23
#define MAX2871_MASK_FB			0x00000001
#define MAX2871_BITM_FB			(MAX2871_MASK_FB<<MAX2871_BITP_FB)
/**
 *	@brief	RFOUT_ Output Divider Mode
 *	@remark	Sets RFOUT_ output divider mode. 
 *			Double buffered by register 0 when REG4DB = 1.
 *			000 = Divide by 1, if 3000MHz ≤ fRFOUTA ≤ 6000MHz
 *			001 = Divide by 2, if 1500MHz ≤ fRFOUTA< 3000MHz
 *			010 = Divide by 4, if 750MHz ≤ fRFOUTA < 1500MHz
 *			011 = Divide by 8, if 375MHz ≤ fRFOUTA < 750MHz
 *			100 = Divide by 16, if 187.5MHz ≤ fRFOUTA < 375MHz
 *			101 = Divide by 32, if 93.75MHz ≤ fRFOUTA < 187.5MHz
 *			110 = Divide by 64, if 46.875MHz ≤ fRFOUTA < 93.75MHz
 *			111 = Divide by 128, if 23.5MHz ≤ fRFOUTA< 46.875MHz
 */	
#define MAX2871_REG_DIVA		MAX2871_REG4
#define MAX2871_BITP_DIVA		20
#define MAX2871_MASK_DIVA		0x00000007
#define MAX2871_BITM_DIVA		(MAX2871_MASK_DIVA<<MAX2871_BITP_DIVA)
/**
 *	@brief	Band Select
 *	@remark	Sets band select clock divider value. MSB are located in bits [25:24].
 *			0000000000 = Reserved
 *			0000000001 =1
 *			0000000010 = 2
 *			----
 *			1111111111 = 1023
 */	
#define MAX2871_REG_BS_L		MAX2871_REG4
#define MAX2871_BITP_BS_L		12
#define MAX2871_MASK_BS_L		0x000000FF
#define MAX2871_BITM_BS_L		(MAX2871_MASK_BS_L<<MAX2871_BITP_BS_L)
/**
 *	@brief	VCO Shutdown
 *	@remark	Sets VCO Shutdown mode:
 *			0 = Enables VCO;
 *			1 = Disables VCO.
 */	
#define MAX2871_REG_SDVCO		MAX2871_REG4
#define MAX2871_BITP_SDVCO		11
#define MAX2871_MASK_SDVCO		0x00000001
#define MAX2871_BITM_SDVCO		(MAX2871_MASK_SDVCO<<MAX2871_BITP_SDVCO)
/**
 *	@brief	RFOUT Mute until Lock Detect
 *	@remark	Sets RFOUT Mute until Lock Detect Mode:
 *			0 = Disables RFOUT Mute until Lock Detect Mode;
 *			1 = Enables RFOUT Mute until Lock Detect Mode.
 */
#define MAX2871_REG_MTLD		MAX2871_REG4
#define MAX2871_BITP_MTLD		10
#define MAX2871_MASK_MTLD		0x00000001
#define MAX2871_BITM_MTLD		(MAX2871_MASK_MTLD<<MAX2871_BITP_MTLD)
/**
 *	@brief	RFOUTB Output Path Select.
 *	@remark	Sets RFOUTB output path select:
 *			0 = VCO divided output;
 *			1 = VCO fundamental frequency.
 */	
#define MAX2871_REG_BDIV		MAX2871_REG4
#define MAX2871_BITP_BDIV		9
#define MAX2871_MASK_BDIV		0x00000001
#define MAX2871_BITM_BDIV		(MAX2871_MASK_BDIV<<MAX2871_BITP_BDIV)
/**
 *	@brief	RFOUTB Output Mode
 *	@remark	Sets RFOUTB output mode:
 *			0 = Disabled;
 *			1 = Enabled.
 */	
#define MAX2871_REG_RFB_EN		MAX2871_REG4
#define MAX2871_BITP_RFB_EN		8
#define MAX2871_MASK_RFB_EN		0x00000001
#define MAX2871_BITM_RFB_EN		(MAX2871_MASK_RFB_EN<<MAX2871_BITP_RFB_EN)
/**
 *	@brief	RFOUTB Output Power
 *	@remark	Sets RFOUTB single-ended output power. See the RFOUTA± and RFOUTB± section:
 *			00 = -4dBm;
 *			01 = -1dBm;
 *			10 = +2dBm;
 *			11 = +5dBm.
 */	
#define MAX2871_REG_BPWR		MAX2871_REG4
#define MAX2871_BITP_BPWR		6
#define MAX2871_MASK_BPWR		0x00000003
#define MAX2871_BITM_BPWR		(MAX2871_MASK_BPWR<<MAX2871_BITP_BPWR)
/**
 *	@brief	RFOUTA Output Mode
 *	@remark	Sets RFOUTA output mode:
 *			0 = Disabled;
 *			1 = Enabled.
 */	
#define MAX2871_REG_RFA_EN		MAX2871_REG4
#define MAX2871_BITP_RFA_EN		5
#define MAX2871_MASK_RFA_EN		0x00000001
#define MAX2871_BITM_RFA_EN		(MAX2871_MASK_RFA_EN<<MAX2871_BITP_RFA_EN)
/**
 *	@brief	RFOUTA Output Power
 *	@remark	Sets RFOUTA single-ended output power. See the RFOUTA± and RFOUTB± section:
 *			00 = -4dBm;
 *			01 = -1dBm;
 *			10 = +2dBm;
 *			11 = +5dBm.
 */	
#define MAX2871_REG_APWR		MAX2871_REG4
#define MAX2871_BITP_APWR		3
#define MAX2871_MASK_APWR		0x00000003
#define MAX2871_BITM_APWR		(MAX2871_MASK_APWR<<MAX2871_BITP_APWR)
//}
/***********************************************************************
 *		REGISTER 5
 ***********************************************************************/
//{
/**
 *	@brief	VAS_DLY
 *	@remark	VCO Autoselect Delay:
 *			Program to 11 when VAS_TEMP=1;
 *			Program to 00 when VAS_TEMP=0.
 */	
#define MAX2871_REG_VAS_DLY		MAX2871_REG5
#define MAX2871_BITP_VAS_DLY	29
#define MAX2871_MASK_VAS_DLY	0x00000003
#define MAX2871_BITM_VAS_DLY	(MAX2871_MASK_VAS_DLY<<MAX2871_BITP_VAS_DLY)
/**
 *	@brief	Shutdown PLL
 *	@remark	Sets Shutdown PLL mode:
 *			0 = Enables PLL;
 *			1 = Disables PLL.
 */	
#define MAX2871_REG_SDPLL		MAX2871_REG5
#define MAX2871_BITP_SDPLL		25
#define MAX2871_MASK_SDPLL		0x00000001
#define MAX2871_BITM_SDPLL		(MAX2871_MASK_SDPLL<<MAX2871_BITP_SDPLL)
/**
 *	@brief	F01
 *	@remark	Sets integer mode for F = 0.
 *			0 = If F[11:0] = 0, then fractional-N mode is set;
 *			1 = If F[11:0] = 0, then integer-N mode is auto set.
 */	
#define MAX2871_REG_F01			MAX2871_REG5
#define MAX2871_BITP_F01		24
#define MAX2871_MASK_F01		0x00000001
#define MAX2871_BITM_F01		(MAX2871_MASK_F01<<MAX2871_BITP_F01)
/**
 *	@brief	Lock-Detect Pin Function
 *	@remark	Sets lock-detect pin function.
 *			00 = Low;
 *			01 = Digital lock detect;
 *			10 = Analog lock detect;
 *			11 = High.
 */	
#define MAX2871_REG_LD			MAX2871_REG5
#define MAX2871_BITP_LD			22
#define MAX2871_MASK_LD			0x00000003
#define MAX2871_BITM_LD			(MAX2871_MASK_LD<<MAX2871_BITP_LD)
/**
 *	@brief	MUX MSB
 *	@remark	Sets mode at MUX pin (see register 2 [28:26]).
 */	
#define MAX2871_REG_MUX_B3		MAX2871_REG5
#define MAX2871_BITP_MUX_B3		18
#define MAX2871_MASK_MUX_B3		0x00000001
#define MAX2871_BITM_MUX_B3		(MAX2871_MASK_MUX_B3<<MAX2871_BITP_MUX_B3)
/**
 *	@brief	ADC Start
 *	@remark	Sets ADC Start mode.
 *			0 = ADC normal operation.
 *			1 = Start ADC conversion process.
 */	
#define MAX2871_REG_ADCS		MAX2871_REG5
#define MAX2871_BITP_ADCS		6
#define MAX2871_MASK_ADCS		0x00000001
#define MAX2871_BITM_ADCS		(MAX2871_MASK_ADCS<<MAX2871_BITP_ADCS)
/**
 *	@brief	ADC Mode
 *	@remark	Sets ADC mode.
 *			000 = Disabled;
 *			001 = Temperature sensor;
 *			010 = Reserved;
 *			011 = Reserved;
 *			100 = Tune pin;
 *			101 = Reserved;
 *			110 = Reserved;
 *			111 = Reserved.
 */	
#define MAX2871_REG_ADCM		MAX2871_REG5
#define MAX2871_BITP_ADCM		3
#define MAX2871_MASK_ADCM		0x00000007
#define MAX2871_BITM_ADCM		(MAX2871_MASK_ADCM<<MAX2871_BITP_ADCM)
//}
/***********************************************************************
 *		REGISTER 6
 ***********************************************************************/
//{
/**
 *	@brief	Die ID.
 *	@remark Die ID.
 *			0110 = MAX2870
 *			0111 = MAX2871
 */
#define MAX2871_REG_DIE			MAX2871_REG6
#define MAX2871_BITP_DIE		28
#define MAX2871_MASK_DIE		0x0000000F
#define MAX2871_BITM_DIE		(MAX2871_MASK_DIE<<MAX2871_BITP_DIE)
/**
 *	@brief	Power On Reset
 *	@remark	Power-On-Reset
 *			0 = Power has not been cycled since last read.
 *			1 = Power has not been cycled since last read. 
 *			All registers have been reset to default values.
 */
#define MAX2871_REG_POR			MAX2871_REG6
#define MAX2871_BITP_POR		23
#define MAX2871_MASK_POR		0x00000001
#define MAX2871_BITM_POR		(MAX2871_MASK_POR<<MAX2871_BITP_POR)
/**
 *	@brief	ADC Code.
 */
#define MAX2871_REG_ADC			MAX2871_REG6
#define MAX2871_BITP_ADC		16
#define MAX2871_MASK_ADC		0x0000007F
#define MAX2871_BITM_ADC		(MAX2871_MASK_ADC<<MAX2871_BITP_ADC)
/**
 *	@brief	ADC Valid
 *	@remark	Determines ADC code validity.
 *			0 = Invalid ADC code.
 *			1 = Valid ADC code.
 */
#define MAX2871_REG_ADCV		MAX2871_REG6
#define MAX2871_BITP_ADCV		15
#define MAX2871_MASK_ADCV		0x00000001
#define MAX2871_BITM_ADCV		(MAX2871_MASK_ADCV<<MAX2871_BITP_ADCV)
/**
 *	@brief	VAS Active
 *	@remark	Determines if VAS is Active:
 *			0 = VCO Autoselect complete;
 *			1 = VCO Autoselect searching for correct VCO.
 */
#define MAX2871_REG_VASA		MAX2871_REG6
#define MAX2871_BITP_VASA		9
#define MAX2871_MASK_VASA		0x00000001
#define MAX2871_BITM_VASA		(MAX2871_MASK_VASA<<MAX2871_BITP_VASA)
/**
 * @brief	Current VCO.
 */
#define MAX2871_REG_V			MAX2871_REG6
#define MAX2871_BITP_V			3
#define MAX2871_MASK_V			0x0000003F
#define MAX2871_BITM_V			(MAX2871_MASK_V<<MAX2871_BITP_V)
//}

#endif	//	__H_SPECIFICATION_MAX2871_
