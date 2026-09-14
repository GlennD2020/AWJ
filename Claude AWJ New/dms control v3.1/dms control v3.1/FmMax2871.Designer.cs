namespace dms_control_v3
{
    partial class FmMax2871
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tbctrlMax2871 = new System.Windows.Forms.TabControl();
            this.tbpgRegisters = new System.Windows.Forms.TabPage();
            this.listView1 = new System.Windows.Forms.ListView();
            this.tbpgParameters = new System.Windows.Forms.TabPage();
            this.cmbbxBPWR = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.chbxRFB_EN = new System.Windows.Forms.CheckBox();
            this.chbxRFA_EN = new System.Windows.Forms.CheckBox();
            this.cmbbxAPWR = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.cmbbxMuxout = new System.Windows.Forms.ComboBox();
            this.lbMuxout = new System.Windows.Forms.Label();
            this.chbxDoubleBuffer = new System.Windows.Forms.CheckBox();
            this.lbChargePumpValue = new System.Windows.Forms.Label();
            this.txbxChargePump = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.txbxRdivider = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.btClose = new System.Windows.Forms.Button();
            this.btApply = new System.Windows.Forms.Button();
            this.tbctrlMax2871.SuspendLayout();
            this.tbpgRegisters.SuspendLayout();
            this.tbpgParameters.SuspendLayout();
            this.SuspendLayout();
            // 
            // tbctrlMax2871
            // 
            this.tbctrlMax2871.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbctrlMax2871.Controls.Add(this.tbpgParameters);
            this.tbctrlMax2871.Controls.Add(this.tbpgRegisters);
            this.tbctrlMax2871.Location = new System.Drawing.Point(1, 3);
            this.tbctrlMax2871.Name = "tbctrlMax2871";
            this.tbctrlMax2871.SelectedIndex = 0;
            this.tbctrlMax2871.Size = new System.Drawing.Size(451, 310);
            this.tbctrlMax2871.TabIndex = 3;
            this.tbctrlMax2871.SelectedIndexChanged += new System.EventHandler(this.tbctrlMax2871_SelectedIndexChanged);
            // 
            // tbpgRegisters
            // 
            this.tbpgRegisters.Controls.Add(this.listView1);
            this.tbpgRegisters.Location = new System.Drawing.Point(4, 22);
            this.tbpgRegisters.Name = "tbpgRegisters";
            this.tbpgRegisters.Padding = new System.Windows.Forms.Padding(3);
            this.tbpgRegisters.Size = new System.Drawing.Size(443, 284);
            this.tbpgRegisters.TabIndex = 0;
            this.tbpgRegisters.Text = "Registers";
            this.tbpgRegisters.UseVisualStyleBackColor = true;
            // 
            // listView1
            // 
            this.listView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listView1.Location = new System.Drawing.Point(6, 6);
            this.listView1.Name = "listView1";
            this.listView1.Size = new System.Drawing.Size(431, 272);
            this.listView1.TabIndex = 1;
            this.listView1.UseCompatibleStateImageBehavior = false;
            // 
            // tbpgParameters
            // 
            this.tbpgParameters.Controls.Add(this.cmbbxBPWR);
            this.tbpgParameters.Controls.Add(this.label4);
            this.tbpgParameters.Controls.Add(this.chbxRFB_EN);
            this.tbpgParameters.Controls.Add(this.chbxRFA_EN);
            this.tbpgParameters.Controls.Add(this.cmbbxAPWR);
            this.tbpgParameters.Controls.Add(this.label3);
            this.tbpgParameters.Controls.Add(this.cmbbxMuxout);
            this.tbpgParameters.Controls.Add(this.lbMuxout);
            this.tbpgParameters.Controls.Add(this.chbxDoubleBuffer);
            this.tbpgParameters.Controls.Add(this.lbChargePumpValue);
            this.tbpgParameters.Controls.Add(this.txbxChargePump);
            this.tbpgParameters.Controls.Add(this.label2);
            this.tbpgParameters.Controls.Add(this.txbxRdivider);
            this.tbpgParameters.Controls.Add(this.label1);
            this.tbpgParameters.Location = new System.Drawing.Point(4, 22);
            this.tbpgParameters.Name = "tbpgParameters";
            this.tbpgParameters.Padding = new System.Windows.Forms.Padding(3);
            this.tbpgParameters.Size = new System.Drawing.Size(443, 284);
            this.tbpgParameters.TabIndex = 1;
            this.tbpgParameters.Text = "Parameters";
            this.tbpgParameters.UseVisualStyleBackColor = true;
            // 
            // cmbbxBPWR
            // 
            this.cmbbxBPWR.FormattingEnabled = true;
            this.cmbbxBPWR.Location = new System.Drawing.Point(244, 118);
            this.cmbbxBPWR.Name = "cmbbxBPWR";
            this.cmbbxBPWR.Size = new System.Drawing.Size(64, 21);
            this.cmbbxBPWR.TabIndex = 13;
            this.cmbbxBPWR.Tag = "BPWR";
            this.cmbbxBPWR.SelectedIndexChanged += new System.EventHandler(this.cmbbxPWR_SelectedIndexChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(194, 122);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(40, 13);
            this.label4.TabIndex = 12;
            this.label4.Text = "BPWR";
            // 
            // chbxRFB_EN
            // 
            this.chbxRFB_EN.AutoSize = true;
            this.chbxRFB_EN.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.chbxRFB_EN.Location = new System.Drawing.Point(192, 93);
            this.chbxRFB_EN.Name = "chbxRFB_EN";
            this.chbxRFB_EN.Size = new System.Drawing.Size(68, 17);
            this.chbxRFB_EN.TabIndex = 11;
            this.chbxRFB_EN.Tag = "RFB_EN";
            this.chbxRFB_EN.Text = "RFB_EN";
            this.chbxRFB_EN.UseVisualStyleBackColor = true;
            this.chbxRFB_EN.Click += new System.EventHandler(this.chbxRF_EN_Click);
            // 
            // chbxRFA_EN
            // 
            this.chbxRFA_EN.AutoSize = true;
            this.chbxRFA_EN.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.chbxRFA_EN.Location = new System.Drawing.Point(51, 93);
            this.chbxRFA_EN.Name = "chbxRFA_EN";
            this.chbxRFA_EN.Size = new System.Drawing.Size(68, 17);
            this.chbxRFA_EN.TabIndex = 10;
            this.chbxRFA_EN.Tag = "RFA_EN";
            this.chbxRFA_EN.Text = "RFA_EN";
            this.chbxRFA_EN.UseVisualStyleBackColor = true;
            this.chbxRFA_EN.Click += new System.EventHandler(this.chbxRF_EN_Click);
            // 
            // cmbbxAPWR
            // 
            this.cmbbxAPWR.FormattingEnabled = true;
            this.cmbbxAPWR.Location = new System.Drawing.Point(104, 117);
            this.cmbbxAPWR.Name = "cmbbxAPWR";
            this.cmbbxAPWR.Size = new System.Drawing.Size(64, 21);
            this.cmbbxAPWR.TabIndex = 9;
            this.cmbbxAPWR.Tag = "APWR";
            this.cmbbxAPWR.SelectedIndexChanged += new System.EventHandler(this.cmbbxPWR_SelectedIndexChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(54, 121);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(40, 13);
            this.label3.TabIndex = 8;
            this.label3.Text = "APWR";
            // 
            // cmbbxMuxout
            // 
            this.cmbbxMuxout.FormattingEnabled = true;
            this.cmbbxMuxout.Location = new System.Drawing.Point(103, 146);
            this.cmbbxMuxout.Name = "cmbbxMuxout";
            this.cmbbxMuxout.Size = new System.Drawing.Size(115, 21);
            this.cmbbxMuxout.TabIndex = 7;
            this.cmbbxMuxout.SelectedIndexChanged += new System.EventHandler(this.cmbbxMuxout_SelectedIndexChanged);
            // 
            // lbMuxout
            // 
            this.lbMuxout.AutoSize = true;
            this.lbMuxout.Location = new System.Drawing.Point(53, 150);
            this.lbMuxout.Name = "lbMuxout";
            this.lbMuxout.Size = new System.Drawing.Size(45, 13);
            this.lbMuxout.TabIndex = 6;
            this.lbMuxout.Text = "Muxout:";
            // 
            // chbxDoubleBuffer
            // 
            this.chbxDoubleBuffer.AutoSize = true;
            this.chbxDoubleBuffer.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.chbxDoubleBuffer.Location = new System.Drawing.Point(28, 70);
            this.chbxDoubleBuffer.Name = "chbxDoubleBuffer";
            this.chbxDoubleBuffer.Size = new System.Drawing.Size(91, 17);
            this.chbxDoubleBuffer.TabIndex = 5;
            this.chbxDoubleBuffer.Text = "Double Buffer";
            this.chbxDoubleBuffer.UseVisualStyleBackColor = true;
            // 
            // lbChargePumpValue
            // 
            this.lbChargePumpValue.AutoSize = true;
            this.lbChargePumpValue.Location = new System.Drawing.Point(178, 47);
            this.lbChargePumpValue.Name = "lbChargePumpValue";
            this.lbChargePumpValue.Size = new System.Drawing.Size(40, 13);
            this.lbChargePumpValue.TabIndex = 4;
            this.lbChargePumpValue.Text = "0.0 mA";
            // 
            // txbxChargePump
            // 
            this.txbxChargePump.Location = new System.Drawing.Point(104, 43);
            this.txbxChargePump.Name = "txbxChargePump";
            this.txbxChargePump.Size = new System.Drawing.Size(63, 20);
            this.txbxChargePump.TabIndex = 3;
            this.txbxChargePump.Tag = "CPG";
            this.txbxChargePump.Text = "1";
            this.txbxChargePump.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.txbxChargePump.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.IntParam_KeyPress);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(22, 46);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(76, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Charge pump :";
            // 
            // txbxRdivider
            // 
            this.txbxRdivider.Location = new System.Drawing.Point(104, 16);
            this.txbxRdivider.Name = "txbxRdivider";
            this.txbxRdivider.Size = new System.Drawing.Size(63, 20);
            this.txbxRdivider.TabIndex = 1;
            this.txbxRdivider.Tag = "Rdiv";
            this.txbxRdivider.Text = "1";
            this.txbxRdivider.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.txbxRdivider.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.IntParam_KeyPress);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(43, 19);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(55, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "R divider :";
            // 
            // btClose
            // 
            this.btClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btClose.Location = new System.Drawing.Point(364, 319);
            this.btClose.Name = "btClose";
            this.btClose.Size = new System.Drawing.Size(75, 23);
            this.btClose.TabIndex = 4;
            this.btClose.Text = "Close";
            this.btClose.UseVisualStyleBackColor = true;
            this.btClose.Click += new System.EventHandler(this.btClose_Click);
            // 
            // btApply
            // 
            this.btApply.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btApply.Location = new System.Drawing.Point(283, 319);
            this.btApply.Name = "btApply";
            this.btApply.Size = new System.Drawing.Size(75, 23);
            this.btApply.TabIndex = 5;
            this.btApply.Text = "Apply";
            this.btApply.UseVisualStyleBackColor = true;
            this.btApply.Click += new System.EventHandler(this.btApply_Click);
            // 
            // FmMax2871
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(451, 352);
            this.Controls.Add(this.btApply);
            this.Controls.Add(this.btClose);
            this.Controls.Add(this.tbctrlMax2871);
            this.Name = "FmMax2871";
            this.Text = "FmMax2871";
            this.tbctrlMax2871.ResumeLayout(false);
            this.tbpgRegisters.ResumeLayout(false);
            this.tbpgParameters.ResumeLayout(false);
            this.tbpgParameters.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tbctrlMax2871;
        private System.Windows.Forms.TabPage tbpgRegisters;
        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.TabPage tbpgParameters;
        private System.Windows.Forms.CheckBox chbxDoubleBuffer;
        private System.Windows.Forms.Label lbChargePumpValue;
        private System.Windows.Forms.TextBox txbxChargePump;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txbxRdivider;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btClose;
        private System.Windows.Forms.Button btApply;
        private System.Windows.Forms.ComboBox cmbbxMuxout;
        private System.Windows.Forms.Label lbMuxout;
        private System.Windows.Forms.CheckBox chbxRFA_EN;
        private System.Windows.Forms.ComboBox cmbbxAPWR;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox cmbbxBPWR;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.CheckBox chbxRFB_EN;
    }
}