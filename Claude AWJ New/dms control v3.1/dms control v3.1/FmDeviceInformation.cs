/**
 * File:    FmDeviceInformation.cs
 * Author:  Kirillov A.V.
 */
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
    public partial class FmDeviceInformation : Form
    {
        /// <summary> Серийный номер. </summary>
        public String sSerialNumber
        {
            set
            {
                txbxSerialNumber.Text = value;
            }
            get
            {
                return txbxSerialNumber.Text;
            }
        }

        /// <summary> Дата производства. </summary>
        public String sDateManufacture
        {
            set 
            {
                char[] buff = value.ToCharArray();
                String Date = String.Format ( "{0:s}{1:s}'.'{2:s}{3:s}'.'{4:s}{5:s}{6:s}{7:s}", buff[0],buff[1],buff[2],buff[3],buff[4],buff[5],buff[6],buff[7] );
                mskDate.Text = Date; 
            }
            get 
            {
                String[] Date = mskDate.Text.Split('.');
                return String.Format ( "{0:00}{1:00}{2:0000}", Date[0], Date[1], Date[2] );
            }
        }

        /// <summary> Конструктор класса. </summary>
        public FmDeviceInformation  ( )
        {
            InitializeComponent();
        }

        /// <summary></summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btApply_Click  ( object sender, EventArgs e )
        {
            DialogResult = System.Windows.Forms.DialogResult.OK;
        }

        /// <summary></summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btCancel_Click ( object sender, EventArgs e )
        {
            DialogResult = System.Windows.Forms.DialogResult.Cancel;
        }
    }
}
