/********************************************************************************
 *	@file:		syscom.h
 *	@brief:		Command system for 6 GHz DMS synthesizer with mixer.
 *	@version:	v1.1 — K9 TDM commands added
 *	@author:	Kirillov A.V. / K9 Electronics
 *	@date:		10/12/2019
 ********************************************************************************/
#ifndef __H_DMS_SYNTHESIZER_SYSCOM_
#define __H_DMS_SYNTHESIZER_SYSCOM_

#define HEAD_OFFSET	6

#pragma pack ( push,1 )
/**
 *	@brief Standard command structure.
 */
typedef struct 
{
	short Type;				//	0 ... 1
	short Status;			//	2 ... 3
	short Len;				//	4 ... 5
	unsigned char data[26];	//	6 ... 31
}tsSysCmd;
#pragma pack(pop)

#define SYSCOM_SIZE	sizeof(tsSysCmd)

/**
 *	@brief	Command list.
 */
enum
{
    TEST_CONNECT = 1,		//  Connection test.
    GET_VERSIONS,			//  Read firmware versions.
    GET_STATE,              //  Read device state.
    READ_MEMORY,			//  Read memory.
    WRITE_MEMORY,			//  Write memory.
    GET_PLL_REG,			//  Read PLL register.
    SET_PLL_REG,			//  Write PLL register.
    GET_DAC_VALUE,			//  Read DAC value.
    SET_DAC_VALUE,			//  Write DAC value.
    GET_SWEEP_PLL,			//  Read PLL sweep params.
    SET_SWEEP_PLL,			//  Set PLL sweep params.
    GET_PULSE_CTRL,			//  Read pulse modulator.
    SET_PULSE_CTRL,			//  Set pulse modulator.
    GET_AMPL_MOD_CTRL,		//  Read amplitude modulation.
    SET_AMPL_MOD_CTRL,		//  Set amplitude modulation.
    GET_CTRL_POWER_DET,		//  Read power detector limit.
    SET_CTRL_POWER_DET,		//  Set power detector limit.
    GET_DIP_FREQ,			//  Read DIP switch frequency.
    SET_DIP_FREQ,			//  Set DIP switch frequency.
    GET_TIME_INTERVAL,		//  Read time intervals.
    SET_TIME_INTERVAL,		//  Set time intervals.
    GET_DDS_REG,			//  Read DDS register.
    SET_DDS_REG,			//  Write DDS register.
    GET_DDS_CTR_FREQ,		//  Read DDS control frequency.
    SET_DDS_CTR_FREQ,		//  Set DDS control frequency.
    GET_SWEEP_DDS,			//	Read DDS sweep params.
    SET_SWEEP_DDS,			//	Set DDS sweep params.
    GET_RAMP_DDS,			//	Read DDS ramp config.
    SET_RAMP_DDS,			//	Set DDS ramp config.
    RESET_DDS,				//	Reset DDS chip.
    DEFAULT,				//  Reset to defaults.

    /* ── K9 TDM Commands (values match AWJ C# application) ── */
    CMD_TDM_START = 44,		//  44: Start TDM band hopping.
    CMD_TDM_STOP  = 45,	//  45: Stop TDM band hopping.
    CMD_TDM_STATUS = 46,	//  46: Read TDM status.
};

/**
 *	@brief	Command execution status.
 */
enum
{
	CMD_NO_ERROR = 0,		//  0 -	No error.
	CMD_DONE, 				//  1 - Command done.
	CMD_UNDEFINED,			//  2 - Command undefined.
	CMD_DEVICE_ERROR,		//  3 -	Device error.
	CMD_RECV_CMD_ERROR,		//  4 -	Receive command error.
	CMD_EEPROM_BUSY,		//  5 -	EEPROM busy.
	CMD_WRITE_EEPROM_ERROR,	//  6 -	EEPROM write error.
	CMD_READ_EEPROM_ERROR,	//  7 -	EEPROM read error.
};

#endif	//	__H_DMS_SYNTHESIZER_SYSCOM_
