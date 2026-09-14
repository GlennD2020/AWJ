/***********************************************************************
 *	@file:		max2871.h
 *	@brief:		Испольнительный файл библиотеки для работы с MAX2871.
 *	@version:	v1.0
 *  
 *	@author:	Kirillov A.V.
 *	@date:		21/05/2019
 ***********************************************************************/
#define __C_MAX2871_
/***********************************************************************
 *			INCLUDES
 ***********************************************************************/
#include "max2871.h"

/***********************************************************************
 *			REGISTERS
 ***********************************************************************/
/**
 *	@brief		MAX2871_Registers
 *	@remark		Массив для инициализации микросхемы.
 */
unsigned long MAX2871_Registers []={
#if MAX2871_REF_FREQ==24000000
	0x00640008,	//	Register 0
	0x20007D01,	//	Register 1
	0x08005E42,	//	Register 2
	0x0000800B,	//	Register 3
	0x619E023C,	//	Register 4
	0x00400005,	//	Register 5
	0x00000006	//	Register 6
#elif MAX2871_REF_FREQ==50000000
	0x00300000,	//	Register 0
	0x20007D01,	//	Register 1
	0x80005E42,	//	Register 2
	0x0000000B,	//	Register 3
	0x639E803C,	//	Register 4
	0x01400005,	//	Register 5
	0x00000006	//	Register 6
/*	2.0 GHz
	0x00300008,	//	Register 0
	0x20007D01,	//	Register 1
	0x84005E42,	//	Register 2
	0x0000000B,	//	Register 3
	0x639E802C,	//	Register 4
	0x01400005,	//	Register 5
	0x00000006	//	Register 6
*/
#endif
};
/***********************************************************************
 *			FUNCTIONS
 ***********************************************************************/

/**
 *	@brief 	Функция записи в массив регистров MAX2871.
 *	@param	ulRegister - Значение регистра.
 *	@retval	NONE
 */
void MAX2871_Register		( uint32_t ulRegister )
{
	MAX2871_Registers [ (int)(ulRegister & 0x07) ] = ulRegister;		
}

/**
 *	@brief		MAX2871_WriteRegister
 *	@remark		Функция записи регистра в массив MAX2871_Registers и запись в микросхему через интерфейс SPI.
 *	@param		ulRegister - Записываемый регистр.
 *	@retval		NONE
 */
void MAX2871_SetRegister	( uint32_t ulRegister )
{
	unsigned char data[4];

	MAX2871_Registers [ (int)(ulRegister & 0x07) ] = ulRegister;
	data[0] = ( unsigned char )( ( ulRegister >> 24 ) & 0xFF );
	data[1] = ( unsigned char )( ( ulRegister >> 16 ) & 0xFF );
	data[2] = ( unsigned char )( ( ulRegister >>  8 ) & 0xFF );
	data[3] = ( unsigned char )( ( ulRegister >>  0 ) & 0xFF );

	HAL_GPIO_WritePin ( Max2871_Periph.cs_port, Max2871_Periph.cs_pin, GPIO_PIN_RESET );
	HAL_SPI_Transmit  ( Max2871_Periph.hspi, 	data, 4, 100 );
	HAL_GPIO_WritePin ( Max2871_Periph.cs_port, Max2871_Periph.cs_pin, GPIO_PIN_SET   );
}

/**
 *	@brief		Функция записи регистра через интерфейс SPI.
 *	@param		ulRegister - Записываемый регистр.
 *	@return		NONE
 */
void MAX2871_WriteRegister	( uint32_t ulRegister )
{
	unsigned char data[4];

	data[0] = ( unsigned char )( ( ulRegister >> 24 ) & 0xFF );
	data[1] = ( unsigned char )( ( ulRegister >> 16 ) & 0xFF );
	data[2] = ( unsigned char )( ( ulRegister >>  8 ) & 0xFF );
	data[3] = ( unsigned char )( ( ulRegister >>  0 ) & 0xFF );

	HAL_GPIO_WritePin ( Max2871_Periph.cs_port, Max2871_Periph.cs_pin, GPIO_PIN_RESET );
	HAL_SPI_Transmit  ( Max2871_Periph.hspi, 	data, 4, 100 );
	HAL_GPIO_WritePin ( Max2871_Periph.cs_port, Max2871_Periph.cs_pin, GPIO_PIN_SET   );
}

/**
 *	@brief		Инициализация микросхемы ФАПЧ MAX2871.
 *	@retval		NONE
 */
void MAX2871_Init 			( void )
{	
	Max2871_RefFrequency = MAX2871_REF_FREQ;
	
	// Для подстраховки устанавливаю линию CS в состояние HIGH
	HAL_GPIO_WritePin ( Max2871_Periph.cs_port, Max2871_Periph.cs_pin, GPIO_PIN_SET ); HAL_Delay (10);
	
	//	Отгрузка инициализационной последовательности для микросхемы MAX2871.
	MAX2871_WriteRegister ( MAX2871_Registers [ MAX2871_REG5 ] ); HAL_Delay (5);
	MAX2871_WriteRegister ( MAX2871_Registers [ MAX2871_REG4 ] ); HAL_Delay (5);
	MAX2871_WriteRegister ( MAX2871_Registers [ MAX2871_REG3 ] ); HAL_Delay (5);
	MAX2871_WriteRegister ( MAX2871_Registers [ MAX2871_REG2 ] ); HAL_Delay (5);
	MAX2871_WriteRegister ( MAX2871_Registers [ MAX2871_REG1 ] ); HAL_Delay (5);
	MAX2871_WriteRegister ( MAX2871_Registers [ MAX2871_REG0 ] ); HAL_Delay (50);
	
	MAX2871_WriteRegister ( MAX2871_Registers [ MAX2871_REG5 ] ); HAL_Delay (5);
	MAX2871_WriteRegister ( MAX2871_Registers [ MAX2871_REG4 ] ); HAL_Delay (5);
	MAX2871_WriteRegister ( MAX2871_Registers [ MAX2871_REG3 ] ); HAL_Delay (5);
	MAX2871_WriteRegister ( MAX2871_Registers [ MAX2871_REG2 ] ); HAL_Delay (5);
	MAX2871_WriteRegister ( MAX2871_Registers [ MAX2871_REG1 ] ); HAL_Delay (5);
	MAX2871_WriteRegister ( MAX2871_Registers [ MAX2871_REG0 ] ); HAL_Delay (50);

}

/**
 *	@brief	MAX2871_Set_Periph
 *	@remark	Задание переферии для работы с микросхемой MAX2871.
 *	@param 	cs_port		- Порт линии CS.
 *	@param 	cs_pin 		- Номер вывода линии CS.
 *	@param 	ld_port 	- Порт линии LD.
 *	@param 	ld_pin 		- Номер вывода линии LD.
 *	@param 	muxout_port	- Порт линии MUXOUT.
 *	@param	muxout_pin	- Номер вывода линии MUXOUT.
 *	@param	hspi		- SPI для работы.
 *	@return	NONE
 */
void MAX2871_SetPeriph 		( 
				GPIO_TypeDef* cs_port, 		uint16_t cs_pin, 
				GPIO_TypeDef* ld_port,		uint16_t ld_pin,
				GPIO_TypeDef* muxout_port, 	uint16_t muxout_pin,
				SPI_HandleTypeDef* 	hspi )
{
	Max2871_Periph.cs_pin		= cs_pin;
	Max2871_Periph.cs_port		= cs_port;
	
	Max2871_Periph.ld_pin		= ld_pin;
	Max2871_Periph.ld_port		= ld_port;	
	
	Max2871_Periph.muxout_pin	= muxout_pin;
	Max2871_Periph.muxout_port	= muxout_port;
	
	Max2871_Periph.hspi			= hspi;
}

/**
 *	@brief	MAX2871_GetMuxOut
 *	@remark Опрос состояния вывода MUXOUT микросхемы MAX2871.
 *	@retval	Состояния линии MUXOUT.
 */
int MAX2871_GetMuxOut		( void )
{
	int result = 0;
	GPIO_PinState muxout_state=HAL_GPIO_ReadPin ( Max2871_Periph.muxout_port, Max2871_Periph.muxout_pin );
	result = ( muxout_state==GPIO_PIN_RESET ) ? 0 : 1;
	return result;
}

/**
 *	@brief	MAX2871_GetLD
 *	@remark Опрос состояния вывода LD микросхемы MAX2871.
 *	@retval	Состояние линии захвата: 
 *			[0] - Синтезатор не в захвате.
 *			[1] - Синтезатор в захвате.
 */
int MAX2871_GetLD			( void )
{
	int result = 0;
	GPIO_PinState ld_state=HAL_GPIO_ReadPin ( Max2871_Periph.ld_port, Max2871_Periph.ld_pin );
	result = ( ld_state==GPIO_PIN_RESET ) ? 0 : 1;
	return result; 
}

/**
 *	@brief	MAX2871_GetVcoDiv
 *	@remark	Расчет делителя частоты ГУНа.
 *	@param	freq - Частота на выходе микрохемы MAX2871.
 *	@return	Делитель
 */
int MAX2871_GetVcoDiv 		( double freq )
{
	int div = 0;
	if ( freq>=MAX2871_VCO_FREQ_MIN )
	{
		div = 0;
	}
	else
	{
		if ( ( freq >= MAX2871_VCO_FREQ_MIN/2 ) && ( freq < MAX2871_VCO_FREQ_MIN ) ) 			div = 1;
		else if ( ( freq >= MAX2871_VCO_FREQ_MIN/4   ) && ( freq < MAX2871_VCO_FREQ_MIN/2   ) )	div = 2;
		else if ( ( freq >= MAX2871_VCO_FREQ_MIN/8   ) && ( freq < MAX2871_VCO_FREQ_MIN/4   ) )	div = 3;
		else if ( ( freq >= MAX2871_VCO_FREQ_MIN/16  ) && ( freq < MAX2871_VCO_FREQ_MIN/8   ) )	div = 4;
		else if ( ( freq >= MAX2871_VCO_FREQ_MIN/32  ) && ( freq < MAX2871_VCO_FREQ_MIN/16  ) )	div = 5;
		else if ( ( freq >= MAX2871_VCO_FREQ_MIN/64  ) && ( freq < MAX2871_VCO_FREQ_MIN/32  ) )	div = 6;
		else if ( ( freq >= MAX2871_VCO_FREQ_MIN/128 ) && ( freq < MAX2871_VCO_FREQ_MIN/64  ) )	div = 7;
		else div = 7;
	}
	return div;
}

/**
 *	@brief		Функция установки частоты
 *	@param		fFrequency 	- Частота на выходе микросхемы.
 *	@param		waitLD 		- Флаг управления ожиданием захвата от ФАПЧ.
 *	@return		Статус выполнения операции.
 */
int MAX2871_SetFrequency 	( double dFrequency, int waitLD )
{
	int LD 		= 1;
	int VcoDiv	= 0;
	int N 		= 0;
	int R		= ( MAX2871_Registers[MAX2871_REG2]>>MAX2871_BITP_R ) & MAX2871_MASK_R;
	int Mod 	= ( MAX2871_Registers[MAX2871_REG1]>>MAX2871_BITP_M ) & MAX2871_MASK_M;
	int Mul		= ( MAX2871_Registers[MAX2871_REG2]>>MAX2871_BITP_DBR ) & MAX2871_MASK_DBR;
	int Frac 	= 0;
	double dN 	= 0.0;
	double DivA = 0.0;
	double dMul = pow (2.0, Mul);

	if ( dFrequency>MAX2871_OUT_FREQ_MAX ) dFrequency=MAX2871_OUT_FREQ_MAX;
	if ( dFrequency<MAX2871_OUT_FREQ_MIN ) dFrequency=MAX2871_OUT_FREQ_MIN;

	VcoDiv	= MAX2871_GetVcoDiv ( dFrequency );
	DivA	= pow ( 2.0, (double)VcoDiv );

	dFrequency = (double)( DivA * dFrequency );
	dN		= (dFrequency * ((double)R))/( Max2871_RefFrequency * dMul );
	N 		= ((int)floor(dN));
	N		= N & 0xFFFF;
	if ( dN<(double)N ) {
		N--;
	}
	dN 		= dN - N;

	Frac 	= (int)(dN * (double)Mod);
	Frac	= Frac & 0xFFF;

	if (Frac==0) Frac=1;

	MAX2871_Registers[MAX2871_REG_RFA_EN] = MAX2871_Registers[MAX2871_REG_RFA_EN]&(~MAX2871_BITM_RFA_EN);	
	MAX2871_WriteRegister ( MAX2871_Registers[MAX2871_REG_RFA_EN] );

	MAX2871_Registers[MAX2871_REG4] = MAX2871_Registers[MAX2871_REG4]&(~MAX2871_BITM_DIVA);
	MAX2871_Registers[MAX2871_REG4] = MAX2871_Registers[MAX2871_REG4]|(VcoDiv<<MAX2871_BITP_DIVA);
	MAX2871_WriteRegister ( MAX2871_Registers[MAX2871_REG4] );

	MAX2871_Registers[MAX2871_REG0] = MAX2871_Registers[MAX2871_REG0]&(~(MAX2871_BITM_N|MAX2871_BITM_FRAC));
	MAX2871_Registers[MAX2871_REG0] = MAX2871_Registers[MAX2871_REG0]|((N<<MAX2871_BITP_N)|(Frac<<MAX2871_BITP_FRAC));
	MAX2871_WriteRegister ( MAX2871_Registers[MAX2871_REG0] );

	for ( long j=0; j<5000; j++ ){
	}

	MAX2871_Registers[MAX2871_REG_RFA_EN] = MAX2871_Registers[MAX2871_REG_RFA_EN]|MAX2871_BITM_RFA_EN;	
	MAX2871_WriteRegister ( MAX2871_Registers[MAX2871_REG_RFA_EN] );

	///	Ожидание установки частоты.
	if ( waitLD==1 ) 
	{
		for ( int i=0; i<10000; i++ ) {
			for ( long j=0; j<100000; j++ ) {
			}
			LD=MAX2871_GetLD();
			if ( LD==1 ) break;
		}
	}

	return LD;
}

/**
 *	@brief		Получение текущей частоты микросхемы.
 *	@return		Расчитанное значение частоты.
 *	@remark		Частота сравнения ФАПЧ.
 *				fPFD = fREF x [(1 + DBR)/(R x (1 + RDIV2))]
 */
double MAX2871_GetFrequency ( void )
{
	int R		= ( MAX2871_Registers[MAX2871_REG_R]>>MAX2871_BITP_R ) 			& MAX2871_MASK_R;
	int DBR		= ( MAX2871_Registers[MAX2871_REG_DBR]>>MAX2871_BITP_DBR ) 		& MAX2871_MASK_DBR;
	int N		= ( MAX2871_Registers[MAX2871_REG_N]>>MAX2871_BITP_N ) 			& MAX2871_MASK_N;
	int Mod 	= ( MAX2871_Registers[MAX2871_REG_M]>>MAX2871_BITP_M ) 			& MAX2871_MASK_M;
	int Frac 	= ( MAX2871_Registers[MAX2871_REG_FRAC]>>MAX2871_BITP_FRAC ) 	& MAX2871_MASK_FRAC;
	int DivA	= ( MAX2871_Registers[MAX2871_REG_DIVA]>>MAX2871_BITP_DIVA ) 	& MAX2871_MASK_DIVA;
	int RDIV2	= ( MAX2871_Registers[MAX2871_REG_RDIV2]>>MAX2871_BITP_RDIV2 )	& MAX2871_MASK_RDIV2;
	
	double frequency = MAX2871_REF_FREQ;
	double dDivA	 = pow ( 2.0, (double)DivA );
	double dRfactor	 = (double)(1 + DBR)/ (double)( R * (1 + RDIV2) );
	double dNfactor	 = (double)N + (double)((double)Frac/(double)Mod );
	
	frequency = frequency * dRfactor;
	frequency = frequency * dNfactor;
	frequency = frequency / dDivA;
	
	return frequency;
}

/**
 *	@brief		Функция выключения микросхемы.
 *	@return		NONE.
 */
void MAX2871_Off			( void )
{
	MAX2871_Registers [ MAX2871_REG_SHDN ] &= ~MAX2871_BITM_SHDN;
	MAX2871_Registers [ MAX2871_REG_SHDN ] |= (MAX2871_SHDN<<MAX2871_BITP_SHDN);
	MAX2871_WriteRegister ( MAX2871_Registers[MAX2871_REG_SHDN] );
}

/**
 *	@brief		Функция включения микросхемы.
 *	@return		NONE.
 */
void MAX2871_On				( void )
{
	MAX2871_Registers [ MAX2871_REG_SHDN ] &= ~MAX2871_BITM_SHDN;
	MAX2871_WriteRegister ( MAX2871_Registers[MAX2871_REG_SHDN] );
}

/**
 *	@brief		Статус состояния выхода.
 *	@retval		[0/1] - 
 */
uint16_t MAX2871_RfOutStatus ( void )
{
	return (( MAX2871_Registers [ MAX2871_REG_SHDN ] & MAX2871_BITM_SHDN )>0) ? 0 : 1;
}

/**
 *	@brief		Считывание регистра.
 *	@param [in]	index - номер считываемого регистра.
 *	@retval		Регистр MAX2871.
 */
unsigned long MAX2871_GetRegister ( int index )
{
	return MAX2871_Registers [ index & 0x07 ];
}
