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

namespace dms_control_v3
{
    public partial class FmFrequencySwitch : Form
    {
        DmsDevice device;

        /// <summary> </summary>
        public FmFrequencySwitch (DmsDevice _device) 
        {
            device = _device;

            InitializeComponent();
            InitParameters();
        }

        /// <summary>  </summary>
        private void InitParameters ( ) 
        {
            txbxFrequency1.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue( device.ltSwitchFrequency[0], 3 );
            txbxFrequency2.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue( device.ltSwitchFrequency[1], 3 );
            txbxFrequency3.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue( device.ltSwitchFrequency[2], 3 );
            txbxFrequency4.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue( device.ltSwitchFrequency[3], 3 );
            txbxFrequency5.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue( device.ltSwitchFrequency[4], 3 );
            txbxFrequency6.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue( device.ltSwitchFrequency[5], 3 );
            txbxFrequency7.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue( device.ltSwitchFrequency[6], 3 );
            txbxFrequency8.Text = nsAlexKir.AppConvertions.Functions.DoubleToFrequencyValue( device.ltSwitchFrequency[7], 3 );
        }

        /// <summary></summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        public float getSwitchFrequency ( int tag )
        {
            float freq = 2500000000.0F;
            switch ( tag )
            {
                case 0:
                    freq = (float)nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble ( txbxFrequency1.Text );
                    break;
                case 1:
                    freq = (float)nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble ( txbxFrequency2.Text );
                    break;
                case 2:
                    freq = (float)nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble ( txbxFrequency3.Text );
                    break;
                case 3:
                    freq = (float)nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble ( txbxFrequency4.Text );
                    break;
                case 4:
                    freq = (float)nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble ( txbxFrequency5.Text );
                    break;
                case 5:
                    freq = (float)nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble ( txbxFrequency6.Text );
                    break;
                case 6:
                    freq = (float)nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble ( txbxFrequency7.Text );
                    break;
                case 7:
                    freq = (float)nsAlexKir.AppConvertions.Functions.FrequencyValueToDouble ( txbxFrequency8.Text );
                    break;
            }
            return freq;
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

        /// <summary></summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txbxFrequency_KeyPress ( object sender, KeyPressEventArgs e ) 
        {
            try
            {
                e.KeyChar = nsAlexKir.AppConvertions.Functions.CheckSymbolToFrequencyValue ( e.KeyChar );
            }
            catch ( Exception ex )
            {
                MessageBox.Show ( "Critical error in the [FmBcdSwitchCfg.txbxFrequency_KeyPress]\n" + ex.Message + "\n" + ex.Source,
                    "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error );
            }
        }

    }
}
