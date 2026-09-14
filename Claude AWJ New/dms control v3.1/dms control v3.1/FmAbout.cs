using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace dms_control_v3
{
    public partial class FmAbout : Form
    {
        public FmAbout()
        {
            InitializeComponent();
        }

        /// <summary> Версия прошивки. </summary>
        public String SoftwareVersion
        {
            get
            {
                return lbSoftwareVersion.Text;
            }
            set
            {
                lbSoftwareVersion.Text = value;
            }
        }

        /// <summary> Версия прошивки. </summary>
        public String FirmwareVersion
        {
            get
            {
                return lbFirmwareVersion.Text;
            }
            set
            {
                lbFirmwareVersion.Text = value;
            }
        }

        /// <summary> Аппаратная версия. </summary>
        public String HardwareVersion
        {
            get
            {
                return lbHardwareVersion.Text;
            }
            set
            {
                lbHardwareVersion.Text = value;
            }
        }

        private void btOK_Click ( object sender, EventArgs e )
        {
            DialogResult = System.Windows.Forms.DialogResult.OK;
        }
    }
}
