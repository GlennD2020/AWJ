using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using nsAlexKir.RF_Library;

namespace dms_control_v3
{
    public partial class FmDdsConfiguration : Form
    {
        AD9106 dds;

        /// <summary> Конструктор класса. </summary>
        public FmDdsConfiguration ( AD9106 _dds )
        {
            InitializeComponent();

            dds = _dds;

            InitParameters();

            listView1.View = View.Details;
            listView1.FullRowSelect = true;
            listView1.Font = new Font ( listView1.Font.Name, 12.0F );

            FillingTable(dds);
        }

        /// <summary> </summary>
        private void btClose_Click  ( object sender, EventArgs e )
        {
            DialogResult = System.Windows.Forms.DialogResult.Cancel;
        }

        /// <summary> </summary>
        private void btApply_Click  ( object sender, EventArgs e )
        {
            DialogResult = System.Windows.Forms.DialogResult.OK;
        }

        /// <summary> </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public bool getChannelState ( int channel )
        {
            bool state = false;
            if (channel == 0)
                state = chbxOUT1.Checked;
            else if (channel == 1)
                state = chbxOUT2.Checked;
            else if (channel == 2)
                state = chbxOUT3.Checked;
            else if (channel == 3)
                state = chbxOUT4.Checked;
            return state;
        }

        /// <summary></summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public ushort getChannelPTW ( int channel ) 
        {
            ushort ptw = 0;
            double phase = 0.0;

            if ( channel==0 )
            {
                phase = Convert.ToDouble ( txbxDdsPhaseOut1.Text );
            }
            else if ( channel==1 )
            {
                phase = Convert.ToDouble ( txbxDdsPhaseOut2.Text );
            }
            else if ( channel==2 )
            {
                phase = Convert.ToDouble ( txbxDdsPhaseOut3.Text );
                phase += 180;
            }
            else if ( channel==3 )
            {
                phase = Convert.ToDouble ( txbxDdsPhaseOut4.Text );
                phase += 180;
            }

            if ( phase>360 ) phase -= 360;

            phase   = (phase / 360.0);
            ptw     = (ushort)(phase * 65535.0);

            return ptw;
        }

        /// <summary></summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public ushort getGain ( int channel ) 
        {
            ushort gain = 0;
            if (channel==0)
            {
                gain = Convert.ToUInt16(txbxDdsGainOut1.Text);
            }
            else if (channel==1)
            {
                gain = Convert.ToUInt16(txbxDdsGainOut2.Text);
            }
            else if (channel==2)
            {
                gain = Convert.ToUInt16(txbxDdsGainOut3.Text);
            }
            else if (channel==3)
            {
                gain = Convert.ToUInt16(txbxDdsGainOut4.Text);
            }
            return gain;
        }

        /// <summary> Считывание максимального значения рамп. </summary>
        /// <returns> Максимальное значение перестройки. </returns>
        public ushort getMaxRamp ( )
        {
            return Convert.ToUInt16(txbxDdsMaxValue.Text);
        }

        /// <summary>  </summary>
        /// <returns> Value of the Pattern period register. </returns>
        public ushort getPatternPeriod ( )
        {
            return Convert.ToUInt16(txbxDdsPatternPeriod.Text);
        }

        /// <summary> </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void chbxOUTx_Click ( object sender, EventArgs e )
        {
            try
            {
                string tag = (string)(((CheckBox)sender).Tag);

                switch (tag)
                {
                    case "OUT1":
                        txbxDdsGainOut1.Text = ( ((CheckBox)sender).Checked )?"1000":"0";
                        break;
                    case "OUT2":
                        txbxDdsGainOut2.Text = ( ((CheckBox)sender).Checked )?"1000":"0";
                        break;
                    case "OUT3":
                        txbxDdsGainOut3.Text = ( ((CheckBox)sender).Checked )?"1000":"0";
                        break;
                    case "OUT4":
                        txbxDdsGainOut4.Text = ( ((CheckBox)sender).Checked )?"1000":"0";
                        break;
                }
            }
            catch ( Exception ex )
            {
                MessageBox.Show("Critical error in the [FmChannels>chbxOUT1_Click]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>  </summary>
        private void InitParameters (  ) 
        {
            ushort usVal = 0;
            double dVal = 0.0;

            chbxOUT1.Checked = dds.Channels[0];
            chbxOUT2.Checked = dds.Channels[1];
            chbxOUT3.Checked = dds.Channels[2];
            chbxOUT4.Checked = dds.Channels[3];

            dVal = (double)(dds.getPTW(0));
            dVal = (double)(360.0 / 65535.0) * dVal;
            txbxDdsPhaseOut1.Text = String.Format("{0:f2}", dVal);

            dVal = (double)(dds.getPTW(1));
            dVal = (double)(360.0 / 65535.0) * dVal;
            txbxDdsPhaseOut2.Text = String.Format("{0:f2}", dVal);

            dVal = (double)(dds.getPTW(2));
            dVal = (double)(360.0 / 65535.0) * dVal;
            dVal -= 180;
            txbxDdsPhaseOut3.Text = String.Format("{0:f2}", dVal);

            dVal = (double)(dds.getPTW(3));
            dVal = (double)(360.0 / 65535.0) * dVal;
            dVal -= 180;
            txbxDdsPhaseOut4.Text = String.Format("{0:f2}", dVal);

            usVal = (ushort)((dds.Channels[0]==true)?dds.getDigitalGain(0):0);
            dds.setDigitalGain(0, usVal);
            txbxDdsGainOut1.Text = Convert.ToString(usVal);

            usVal = (ushort)((dds.Channels[1]==true)?dds.getDigitalGain(1):0);
            dds.setDigitalGain(1, usVal);
            txbxDdsGainOut2.Text = Convert.ToString(usVal);

            usVal = (ushort)((dds.Channels[2]==true)?dds.getDigitalGain(2):0);
            dds.setDigitalGain(2, usVal);
            txbxDdsGainOut3.Text = Convert.ToString(usVal);

            usVal = (ushort)((dds.Channels[3]==true)?dds.getDigitalGain(3):0);
            dds.setDigitalGain(3, usVal);
            txbxDdsGainOut4.Text = Convert.ToString(usVal);

            txbxDdsMaxValue.Text = Convert.ToString(dds.MaxRamp);

            txbxDdsPatternPeriod.Text = Convert.ToString(dds.PatternPeriod);
        }

    #region Функции для работы с таблицей регистров

        /// <summary> Заполнение таблицы. </summary>
        /// <param name="pll"> Класс СЧ. </param>
        public void FillingTable            ( AD9106 _dds )
        {
            FillingTable(_dds, listView1);
        }

        /// <summary> Заполнение таблицы. </summary>
        /// <param name="pll"> Класс синтезатора частот. </param>
        /// <param name="lvTable"> Указатель на таблицу. </param>
        public void FillingTable            ( AD9106 _dds, ListView lvTable )
        {
            int row = 0;
            lvTable.Clear();
            lvTable.Columns.Add("name", "Address", (listView1.Width / 3) - 5);
            lvTable.Columns.Add("data", "Date", (2 * listView1.Width / 3) - 20);
            for (int i = 0; i < _dds.registers.Length; i++ )
            {
                WriteTextInTableCell ( lvTable, row, 0, String.Format("0x{0:X2}", i) );
                WriteTextInTableCell ( lvTable, row, 1, String.Format("0x{0:X4}", _dds.registers[i]) );
                ++row;
            }
        }

        /// <summary> Функция для добавления текста в ячейку таблицы. </summary>
        /// <param name="lvTable"> Таблица. </param>
        /// <param name="iRow"> Номер строки. </param>
        /// <param name="iCol"> Номер столбца. </param>
        /// <param name="sText"> Строка. </param>
        /// <returns> Статус выполнения операции. </returns>
        private int WriteTextInTableCell    ( ListView lvTable, int iRow, int iCol, string sText ) 
        {
            try
            {
                if (iRow >= lvTable.Items.Count)
                {
                    lvTable.Items.Add(sText);
                }
                else
                {
                    if (iCol >= lvTable.Items[iRow].SubItems.Count) lvTable.Items[iRow].SubItems.Add(sText);
                    else lvTable.Items[iRow].SubItems[iCol].Text = sText;
                }
            }
            catch
            {
                return -2;
            }
            return 0;
        }
    #endregion

        /// <summary>  </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbCtrlDds_SelectedIndexChanged ( object sender, EventArgs e ) 
        {
            try
            {
                InitParameters  ( );
                FillingTable    ( dds );
            }
            catch ( Exception ex )
            {
                MessageBox.Show ( "Critical error in the [tbctrlDDS_SelectedIndexChanged]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error );
            }
        }

        /// <summary>  </summary>
        private void txbxDdsGainOutx_KeyPress ( object sender, KeyPressEventArgs e ) 
        {
            try
            {
                if ( e.KeyChar==13 )
                {
                    ushort gain = Convert.ToUInt16(((TextBox)sender).Text);
                    String tag  = (String)(((TextBox)sender).Tag);
                    switch ( tag )
                    {
                        case "OUT1":
                            if ( dds.Channels[0]==true )
                                dds.setDigitalGain ( 0, gain );
                            break;
                        case "OUT2":
                            if ( dds.Channels[1]==true )
                                dds.setDigitalGain ( 1, gain );
                            break;
                        case "OUT3":
                            if ( dds.Channels[2]==true )
                                dds.setDigitalGain ( 2, gain );
                            break;
                        case "OUT4":
                            if ( dds.Channels[3]==true )
                                dds.setDigitalGain ( 3, gain );
                            break;
                    }
                }
            }
            catch ( Exception ex )
            {
                MessageBox.Show ( "Critical error in the [txbxDdsGainOutx_KeyPress]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error );
            }
        }

        /// <summary> </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txbxDdsParameters_KeyPress ( object sender, KeyPressEventArgs e ) 
        {
            try
            {
                String tag = (String)(((TextBox)sender).Tag);
                if ( e.KeyChar==13 )
                {                    
                    ((TextBox)sender).BackColor = SystemColors.Window;
                    switch (tag)
                    { 
                        case "MaxValue":
                            dds.MaxRamp = Convert.ToUInt16(((TextBox)sender).Text);
                            break;

                        case "Pattern":
                            dds.PatternPeriod = Convert.ToUInt16(((TextBox)sender).Text);
                            break;
                    }
                }
                else
                {
                    ((TextBox)sender).BackColor = Color.Azure;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show ( "Critical error in the [txbxDdsRamp_KeyPress]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error );
            }
        }

    }
}
