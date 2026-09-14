using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using nsAlexKir;
using nsAlexKir.Synthesizers;

namespace dms_control_v3
{
    public partial class FmMax2871 : Form
    {
        public MAX2871 pll;

        public FmMax2871( MAX2871 _pll )
        {
            InitializeComponent ();

            listView1.View          = View.Details;
            listView1.FullRowSelect = true;
            listView1.Font          = new Font ( listView1.Font.Name, 12.0F );

            pll = new MAX2871();
            pll = _pll;

            FillingTable ( pll );

            InitParametersPanel ( pll );
        }

        /// <summary> Инициализация панели параметров. </summary>
        /// <param name="pll"> Объект синтезатора частот. </param>
        private void InitParametersPanel    ( MAX2871 pll )
        {
            txbxRdivider.Text        = Convert.ToString ( pll.R );
            txbxChargePump.Text      = Convert.ToString ( pll.CP );
            lbChargePumpValue.Text   = String.Format ( "{0:f2} mA", ((1.63/5100.0)*(1+pll.CP))*1000 );
            
            cmbbxMuxout.Items.Clear();
            foreach (string s in pll.sMuxout)
            {
                cmbbxMuxout.Items.Add(s);
            }
            cmbbxMuxout.SelectedIndex = pll.MUXOUT;

            cmbbxAPWR.Items.Clear();
            cmbbxBPWR.Items.Clear();
            foreach (string s in pll.sPower)
            {
                cmbbxAPWR.Items.Add(s);
                cmbbxBPWR.Items.Add(s);
            }
            cmbbxAPWR.SelectedIndex = pll.APWR;
            cmbbxBPWR.SelectedIndex = pll.BPWR;

            chbxDoubleBuffer.Checked = pll.DBR>0 ? true : false;
            chbxRFA_EN.Checked = pll.RFA_EN>0?true:false;
            chbxRFB_EN.Checked = pll.RFB_EN>0?true:false;
        }

    #region Функции для работы с таблицей регистров
        /// <summary> Заполнение таблицы. </summary>
        /// <param name="pll"> Класс СЧ. </param>
        public void FillingTable            ( MAX2871 pll ) 
        {
            FillingTable ( pll, listView1 );
        }
        
        /// <summary> Заполнение таблицы. </summary>
        /// <param name="pll"> Класс синтезатора частот. </param>
        /// <param name="lvTable"> Указатель на таблицу. </param>
        public void FillingTable            ( MAX2871 pll, ListView lvTable ) 
        {
            int row = 0;
            lvTable.Clear();
            lvTable.Columns.Add ( "name", "Name of register", (listView1.Width / 3)-5 );
            lvTable.Columns.Add ( "data", "Date", ( 2*listView1.Width/3)-20 );
            foreach (var item in pll.registers)
            {
                WriteTextInTableCell ( lvTable, row, 0, item.Name );
                WriteTextInTableCell ( lvTable, row, 1, String.Format ( "0x{0:X8}", item.body ) );
                ++row;
            }
        }
        
        /// <summary> Функция для добавления текста в ячейку таблицы. </summary>
        /// <param name="lvTable"> Таблица. </param>
        /// <param name="iRow"> Номер строки. </param>
        /// <param name="iCol"> Номер столбца. </param>
        /// <param name="sText"> Строка. </param>
        /// <returns> Статус выполнения операции. </returns>
        private int WriteTextInTableCell     ( ListView lvTable, int iRow, int iCol, string sText ) 
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
    
        /// <summary> Закрытие окна. </summary>
        private void btClose_Click           ( object sender, EventArgs e ) 
        {
            DialogResult = System.Windows.Forms.DialogResult.Cancel;
        }

        /// <summary> Успешное закрытие формы. </summary>
        private void btApply_Click           ( object sender, EventArgs e )
        {
            DialogResult = System.Windows.Forms.DialogResult.OK;
        }

        /// <summary></summary>
        private void cmbbxMuxout_SelectedIndexChanged(object sender, EventArgs e)
        {
            pll.MUXOUT = cmbbxMuxout.SelectedIndex;
        }

        /// <summary> Функция обработки введение целочисленного параметра. </summary>
        private void IntParam_KeyPress      ( object sender, KeyPressEventArgs e )
        {
            try
            {
                String tag = (String)(((TextBox)sender).Tag);
                if ( e.KeyChar==13 )
                {
                    ((TextBox)sender).BackColor = SystemColors.Window;
                    if (tag=="Rdiv")
                    {
                        pll.R = Convert.ToInt32(((TextBox)sender).Text);
                    }
                    else if (tag=="CPG")
                    {
                        pll.CP = Convert.ToInt32(((TextBox)sender).Text);
                    }
                }
                else
                {
                    ((TextBox)sender).BackColor = Color.Azure;
                    e.KeyChar = nsAlexKir.AppConvertions.Functions.CheckSymbolToIntegerValue ( e.KeyChar );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [MAX2871/IntParam_KeyPress]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>  </summary>
        private void tbctrlMax2871_SelectedIndexChanged(object sender, EventArgs e)
        {
            FillingTable(pll);
        }

        /// <summary>  </summary>
        private void chbxRF_EN_Click    ( object sender, EventArgs e )
        {
            try
            {
                String tag = (String)(((CheckBox)sender).Tag);
                if ( tag=="RFA_EN" )
                {
                    pll.RFA_EN = (((CheckBox)sender).Checked) ? 1 : 0;
                }
                else if ( tag=="RFB_EN" )
                {
                    pll.RFB_EN = (((CheckBox)sender).Checked) ? 1 : 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [MAX2871/chbxRF_EN_Click]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>  </summary>
        private void cmbbxPWR_SelectedIndexChanged ( object sender, EventArgs e )
        {
            try
            {
                String tag = (String)(((ComboBox)sender).Tag);
                if ( tag=="APWR" )
                {
                    pll.APWR = ((ComboBox)sender).SelectedIndex;
                }
                else if ( tag=="BPWR" )
                {
                    pll.BPWR = ((ComboBox)sender).SelectedIndex;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical error in the [MAX2871/cmbbxPWR_SelectedIndexChanged]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    
    }
}
