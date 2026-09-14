namespace dms_control_v3
{
    partial class FmDdsConfiguration
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
            this.btApply = new System.Windows.Forms.Button();
            this.btClose = new System.Windows.Forms.Button();
            this.grbxChannelOut3 = new System.Windows.Forms.GroupBox();
            this.label7 = new System.Windows.Forms.Label();
            this.txbxDdsGainOut3 = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.txbxDdsPhaseOut3 = new System.Windows.Forms.TextBox();
            this.chbxOUT3 = new System.Windows.Forms.CheckBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label8 = new System.Windows.Forms.Label();
            this.txbxDdsGainOut4 = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.txbxDdsPhaseOut4 = new System.Windows.Forms.TextBox();
            this.chbxOUT4 = new System.Windows.Forms.CheckBox();
            this.tbCtrlDds = new System.Windows.Forms.TabControl();
            this.tbpgRegisters = new System.Windows.Forms.TabPage();
            this.listView1 = new System.Windows.Forms.ListView();
            this.tbpgControls = new System.Windows.Forms.TabPage();
            this.grbxChannelOut2 = new System.Windows.Forms.GroupBox();
            this.label6 = new System.Windows.Forms.Label();
            this.txbxDdsGainOut2 = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.txbxDdsPhaseOut2 = new System.Windows.Forms.TextBox();
            this.chbxOUT2 = new System.Windows.Forms.CheckBox();
            this.grbxChannelOut1 = new System.Windows.Forms.GroupBox();
            this.label5 = new System.Windows.Forms.Label();
            this.txbxDdsGainOut1 = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.txbxDdsPhaseOut1 = new System.Windows.Forms.TextBox();
            this.chbxOUT1 = new System.Windows.Forms.CheckBox();
            this.tbpgSettings = new System.Windows.Forms.TabPage();
            this.lbDdsPatternPeriod = new System.Windows.Forms.Label();
            this.lbDdsMaxValue = new System.Windows.Forms.Label();
            this.txbxDdsPatternPeriod = new System.Windows.Forms.TextBox();
            this.txbxDdsMaxValue = new System.Windows.Forms.TextBox();
            this.txbxDdsFTW = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.grbxChannelOut3.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.tbCtrlDds.SuspendLayout();
            this.tbpgRegisters.SuspendLayout();
            this.tbpgControls.SuspendLayout();
            this.grbxChannelOut2.SuspendLayout();
            this.grbxChannelOut1.SuspendLayout();
            this.tbpgSettings.SuspendLayout();
            this.SuspendLayout();
            // 
            // btApply
            // 
            this.btApply.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btApply.Location = new System.Drawing.Point(206, 296);
            this.btApply.Name = "btApply";
            this.btApply.Size = new System.Drawing.Size(75, 23);
            this.btApply.TabIndex = 0;
            this.btApply.Text = "Apply";
            this.btApply.UseVisualStyleBackColor = true;
            this.btApply.Click += new System.EventHandler(this.btApply_Click);
            // 
            // btClose
            // 
            this.btClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btClose.Location = new System.Drawing.Point(287, 296);
            this.btClose.Name = "btClose";
            this.btClose.Size = new System.Drawing.Size(75, 23);
            this.btClose.TabIndex = 1;
            this.btClose.Text = "Close";
            this.btClose.UseVisualStyleBackColor = true;
            this.btClose.Click += new System.EventHandler(this.btClose_Click);
            // 
            // grbxChannelOut3
            // 
            this.grbxChannelOut3.Controls.Add(this.label7);
            this.grbxChannelOut3.Controls.Add(this.txbxDdsGainOut3);
            this.grbxChannelOut3.Controls.Add(this.label3);
            this.grbxChannelOut3.Controls.Add(this.txbxDdsPhaseOut3);
            this.grbxChannelOut3.Controls.Add(this.chbxOUT3);
            this.grbxChannelOut3.Location = new System.Drawing.Point(7, 128);
            this.grbxChannelOut3.Name = "grbxChannelOut3";
            this.grbxChannelOut3.Size = new System.Drawing.Size(331, 55);
            this.grbxChannelOut3.TabIndex = 5;
            this.grbxChannelOut3.TabStop = false;
            this.grbxChannelOut3.Text = "OUT3:";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(95, 24);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(32, 13);
            this.label7.TabIndex = 8;
            this.label7.Text = "Gain:";
            // 
            // txbxDdsGainOut3
            // 
            this.txbxDdsGainOut3.Location = new System.Drawing.Point(132, 21);
            this.txbxDdsGainOut3.Name = "txbxDdsGainOut3";
            this.txbxDdsGainOut3.Size = new System.Drawing.Size(66, 20);
            this.txbxDdsGainOut3.TabIndex = 7;
            this.txbxDdsGainOut3.Tag = "OUT3";
            this.txbxDdsGainOut3.Text = "512";
            this.txbxDdsGainOut3.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.txbxDdsGainOut3.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txbxDdsGainOutx_KeyPress);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(215, 24);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(40, 13);
            this.label3.TabIndex = 2;
            this.label3.Text = "Phase:";
            // 
            // txbxDdsPhaseOut3
            // 
            this.txbxDdsPhaseOut3.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txbxDdsPhaseOut3.Location = new System.Drawing.Point(260, 21);
            this.txbxDdsPhaseOut3.Name = "txbxDdsPhaseOut3";
            this.txbxDdsPhaseOut3.Size = new System.Drawing.Size(60, 20);
            this.txbxDdsPhaseOut3.TabIndex = 1;
            this.txbxDdsPhaseOut3.Text = "0";
            this.txbxDdsPhaseOut3.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // chbxOUT3
            // 
            this.chbxOUT3.AutoSize = true;
            this.chbxOUT3.Location = new System.Drawing.Point(22, 23);
            this.chbxOUT3.Name = "chbxOUT3";
            this.chbxOUT3.Size = new System.Drawing.Size(59, 17);
            this.chbxOUT3.TabIndex = 0;
            this.chbxOUT3.Tag = "OUT3";
            this.chbxOUT3.Text = "Enable";
            this.chbxOUT3.UseVisualStyleBackColor = true;
            this.chbxOUT3.Click += new System.EventHandler(this.chbxOUTx_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label8);
            this.groupBox1.Controls.Add(this.txbxDdsGainOut4);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.txbxDdsPhaseOut4);
            this.groupBox1.Controls.Add(this.chbxOUT4);
            this.groupBox1.Location = new System.Drawing.Point(7, 189);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(331, 55);
            this.groupBox1.TabIndex = 6;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "OUT4:";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(93, 24);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(32, 13);
            this.label8.TabIndex = 8;
            this.label8.Text = "Gain:";
            // 
            // txbxDdsGainOut4
            // 
            this.txbxDdsGainOut4.Location = new System.Drawing.Point(130, 21);
            this.txbxDdsGainOut4.Name = "txbxDdsGainOut4";
            this.txbxDdsGainOut4.Size = new System.Drawing.Size(66, 20);
            this.txbxDdsGainOut4.TabIndex = 7;
            this.txbxDdsGainOut4.Tag = "OUT4";
            this.txbxDdsGainOut4.Text = "512";
            this.txbxDdsGainOut4.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.txbxDdsGainOut4.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txbxDdsGainOutx_KeyPress);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(213, 24);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(40, 13);
            this.label4.TabIndex = 2;
            this.label4.Text = "Phase:";
            // 
            // txbxDdsPhaseOut4
            // 
            this.txbxDdsPhaseOut4.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txbxDdsPhaseOut4.Location = new System.Drawing.Point(260, 21);
            this.txbxDdsPhaseOut4.Name = "txbxDdsPhaseOut4";
            this.txbxDdsPhaseOut4.Size = new System.Drawing.Size(60, 20);
            this.txbxDdsPhaseOut4.TabIndex = 1;
            this.txbxDdsPhaseOut4.Text = "0";
            this.txbxDdsPhaseOut4.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // chbxOUT4
            // 
            this.chbxOUT4.AutoSize = true;
            this.chbxOUT4.Location = new System.Drawing.Point(20, 23);
            this.chbxOUT4.Name = "chbxOUT4";
            this.chbxOUT4.Size = new System.Drawing.Size(59, 17);
            this.chbxOUT4.TabIndex = 0;
            this.chbxOUT4.Tag = "OUT4";
            this.chbxOUT4.Text = "Enable";
            this.chbxOUT4.UseVisualStyleBackColor = true;
            this.chbxOUT4.Click += new System.EventHandler(this.chbxOUTx_Click);
            // 
            // tbCtrlDds
            // 
            this.tbCtrlDds.Controls.Add(this.tbpgRegisters);
            this.tbCtrlDds.Controls.Add(this.tbpgControls);
            this.tbCtrlDds.Controls.Add(this.tbpgSettings);
            this.tbCtrlDds.Location = new System.Drawing.Point(5, 5);
            this.tbCtrlDds.Name = "tbCtrlDds";
            this.tbCtrlDds.SelectedIndex = 0;
            this.tbCtrlDds.Size = new System.Drawing.Size(359, 281);
            this.tbCtrlDds.TabIndex = 7;
            this.tbCtrlDds.SelectedIndexChanged += new System.EventHandler(this.tbCtrlDds_SelectedIndexChanged);
            // 
            // tbpgRegisters
            // 
            this.tbpgRegisters.Controls.Add(this.listView1);
            this.tbpgRegisters.Location = new System.Drawing.Point(4, 22);
            this.tbpgRegisters.Name = "tbpgRegisters";
            this.tbpgRegisters.Padding = new System.Windows.Forms.Padding(3);
            this.tbpgRegisters.Size = new System.Drawing.Size(351, 255);
            this.tbpgRegisters.TabIndex = 1;
            this.tbpgRegisters.Text = "Registers";
            this.tbpgRegisters.UseVisualStyleBackColor = true;
            // 
            // listView1
            // 
            this.listView1.Location = new System.Drawing.Point(6, 6);
            this.listView1.Name = "listView1";
            this.listView1.Size = new System.Drawing.Size(339, 243);
            this.listView1.TabIndex = 0;
            this.listView1.UseCompatibleStateImageBehavior = false;
            // 
            // tbpgControls
            // 
            this.tbpgControls.Controls.Add(this.grbxChannelOut2);
            this.tbpgControls.Controls.Add(this.groupBox1);
            this.tbpgControls.Controls.Add(this.grbxChannelOut1);
            this.tbpgControls.Controls.Add(this.grbxChannelOut3);
            this.tbpgControls.Location = new System.Drawing.Point(4, 22);
            this.tbpgControls.Name = "tbpgControls";
            this.tbpgControls.Padding = new System.Windows.Forms.Padding(3);
            this.tbpgControls.Size = new System.Drawing.Size(351, 255);
            this.tbpgControls.TabIndex = 0;
            this.tbpgControls.Text = "Channels";
            this.tbpgControls.UseVisualStyleBackColor = true;
            // 
            // grbxChannelOut2
            // 
            this.grbxChannelOut2.Controls.Add(this.label6);
            this.grbxChannelOut2.Controls.Add(this.txbxDdsGainOut2);
            this.grbxChannelOut2.Controls.Add(this.label2);
            this.grbxChannelOut2.Controls.Add(this.txbxDdsPhaseOut2);
            this.grbxChannelOut2.Controls.Add(this.chbxOUT2);
            this.grbxChannelOut2.Location = new System.Drawing.Point(7, 67);
            this.grbxChannelOut2.Name = "grbxChannelOut2";
            this.grbxChannelOut2.Size = new System.Drawing.Size(331, 55);
            this.grbxChannelOut2.TabIndex = 5;
            this.grbxChannelOut2.TabStop = false;
            this.grbxChannelOut2.Text = "OUT2:";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(95, 24);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(32, 13);
            this.label6.TabIndex = 6;
            this.label6.Text = "Gain:";
            // 
            // txbxDdsGainOut2
            // 
            this.txbxDdsGainOut2.Location = new System.Drawing.Point(132, 21);
            this.txbxDdsGainOut2.Name = "txbxDdsGainOut2";
            this.txbxDdsGainOut2.Size = new System.Drawing.Size(66, 20);
            this.txbxDdsGainOut2.TabIndex = 5;
            this.txbxDdsGainOut2.Tag = "OUT2";
            this.txbxDdsGainOut2.Text = "512";
            this.txbxDdsGainOut2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.txbxDdsGainOut2.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txbxDdsGainOutx_KeyPress);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(215, 24);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(40, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Phase:";
            // 
            // txbxDdsPhaseOut2
            // 
            this.txbxDdsPhaseOut2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txbxDdsPhaseOut2.Location = new System.Drawing.Point(260, 21);
            this.txbxDdsPhaseOut2.Name = "txbxDdsPhaseOut2";
            this.txbxDdsPhaseOut2.Size = new System.Drawing.Size(60, 20);
            this.txbxDdsPhaseOut2.TabIndex = 1;
            this.txbxDdsPhaseOut2.Text = "0";
            this.txbxDdsPhaseOut2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // chbxOUT2
            // 
            this.chbxOUT2.AutoSize = true;
            this.chbxOUT2.Location = new System.Drawing.Point(22, 23);
            this.chbxOUT2.Name = "chbxOUT2";
            this.chbxOUT2.Size = new System.Drawing.Size(59, 17);
            this.chbxOUT2.TabIndex = 0;
            this.chbxOUT2.Tag = "OUT2";
            this.chbxOUT2.Text = "Enable";
            this.chbxOUT2.UseVisualStyleBackColor = true;
            this.chbxOUT2.Click += new System.EventHandler(this.chbxOUTx_Click);
            // 
            // grbxChannelOut1
            // 
            this.grbxChannelOut1.Controls.Add(this.label5);
            this.grbxChannelOut1.Controls.Add(this.txbxDdsGainOut1);
            this.grbxChannelOut1.Controls.Add(this.label1);
            this.grbxChannelOut1.Controls.Add(this.txbxDdsPhaseOut1);
            this.grbxChannelOut1.Controls.Add(this.chbxOUT1);
            this.grbxChannelOut1.Location = new System.Drawing.Point(7, 6);
            this.grbxChannelOut1.Name = "grbxChannelOut1";
            this.grbxChannelOut1.Size = new System.Drawing.Size(331, 55);
            this.grbxChannelOut1.TabIndex = 3;
            this.grbxChannelOut1.TabStop = false;
            this.grbxChannelOut1.Text = "OUT1:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(96, 24);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(32, 13);
            this.label5.TabIndex = 4;
            this.label5.Text = "Gain:";
            // 
            // txbxDdsGainOut1
            // 
            this.txbxDdsGainOut1.Location = new System.Drawing.Point(133, 21);
            this.txbxDdsGainOut1.Name = "txbxDdsGainOut1";
            this.txbxDdsGainOut1.Size = new System.Drawing.Size(66, 20);
            this.txbxDdsGainOut1.TabIndex = 3;
            this.txbxDdsGainOut1.Tag = "OUT1";
            this.txbxDdsGainOut1.Text = "512";
            this.txbxDdsGainOut1.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.txbxDdsGainOut1.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txbxDdsGainOutx_KeyPress);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(216, 24);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(40, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Phase:";
            // 
            // txbxDdsPhaseOut1
            // 
            this.txbxDdsPhaseOut1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txbxDdsPhaseOut1.Location = new System.Drawing.Point(260, 21);
            this.txbxDdsPhaseOut1.Name = "txbxDdsPhaseOut1";
            this.txbxDdsPhaseOut1.Size = new System.Drawing.Size(60, 20);
            this.txbxDdsPhaseOut1.TabIndex = 1;
            this.txbxDdsPhaseOut1.Text = "0";
            this.txbxDdsPhaseOut1.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // chbxOUT1
            // 
            this.chbxOUT1.AutoSize = true;
            this.chbxOUT1.Location = new System.Drawing.Point(23, 23);
            this.chbxOUT1.Name = "chbxOUT1";
            this.chbxOUT1.Size = new System.Drawing.Size(59, 17);
            this.chbxOUT1.TabIndex = 0;
            this.chbxOUT1.Tag = "OUT1";
            this.chbxOUT1.Text = "Enable";
            this.chbxOUT1.UseVisualStyleBackColor = true;
            this.chbxOUT1.Click += new System.EventHandler(this.chbxOUTx_Click);
            // 
            // tbpgSettings
            // 
            this.tbpgSettings.Controls.Add(this.lbDdsPatternPeriod);
            this.tbpgSettings.Controls.Add(this.label9);
            this.tbpgSettings.Controls.Add(this.lbDdsMaxValue);
            this.tbpgSettings.Controls.Add(this.txbxDdsPatternPeriod);
            this.tbpgSettings.Controls.Add(this.txbxDdsFTW);
            this.tbpgSettings.Controls.Add(this.txbxDdsMaxValue);
            this.tbpgSettings.Location = new System.Drawing.Point(4, 22);
            this.tbpgSettings.Name = "tbpgSettings";
            this.tbpgSettings.Padding = new System.Windows.Forms.Padding(3);
            this.tbpgSettings.Size = new System.Drawing.Size(351, 255);
            this.tbpgSettings.TabIndex = 2;
            this.tbpgSettings.Text = "Settings";
            this.tbpgSettings.UseVisualStyleBackColor = true;
            // 
            // lbDdsPatternPeriod
            // 
            this.lbDdsPatternPeriod.AutoSize = true;
            this.lbDdsPatternPeriod.Location = new System.Drawing.Point(39, 78);
            this.lbDdsPatternPeriod.Name = "lbDdsPatternPeriod";
            this.lbDdsPatternPeriod.Size = new System.Drawing.Size(76, 13);
            this.lbDdsPatternPeriod.TabIndex = 1;
            this.lbDdsPatternPeriod.Text = "Pattern period:";
            // 
            // lbDdsMaxValue
            // 
            this.lbDdsMaxValue.AutoSize = true;
            this.lbDdsMaxValue.Location = new System.Drawing.Point(54, 52);
            this.lbDdsMaxValue.Name = "lbDdsMaxValue";
            this.lbDdsMaxValue.Size = new System.Drawing.Size(60, 13);
            this.lbDdsMaxValue.TabIndex = 1;
            this.lbDdsMaxValue.Text = "Max Value:";
            // 
            // txbxDdsPatternPeriod
            // 
            this.txbxDdsPatternPeriod.Location = new System.Drawing.Point(117, 75);
            this.txbxDdsPatternPeriod.Name = "txbxDdsPatternPeriod";
            this.txbxDdsPatternPeriod.Size = new System.Drawing.Size(100, 20);
            this.txbxDdsPatternPeriod.TabIndex = 0;
            this.txbxDdsPatternPeriod.Tag = "Pattern";
            this.txbxDdsPatternPeriod.Text = "0";
            this.txbxDdsPatternPeriod.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.txbxDdsPatternPeriod.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txbxDdsParameters_KeyPress);
            // 
            // txbxDdsMaxValue
            // 
            this.txbxDdsMaxValue.Location = new System.Drawing.Point(117, 49);
            this.txbxDdsMaxValue.Name = "txbxDdsMaxValue";
            this.txbxDdsMaxValue.Size = new System.Drawing.Size(100, 20);
            this.txbxDdsMaxValue.TabIndex = 0;
            this.txbxDdsMaxValue.Tag = "MaxValue";
            this.txbxDdsMaxValue.Text = "0";
            this.txbxDdsMaxValue.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.txbxDdsMaxValue.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txbxDdsParameters_KeyPress);
            // 
            // txbxDdsFTW
            // 
            this.txbxDdsFTW.Location = new System.Drawing.Point(117, 23);
            this.txbxDdsFTW.Name = "txbxDdsFTW";
            this.txbxDdsFTW.Size = new System.Drawing.Size(100, 20);
            this.txbxDdsFTW.TabIndex = 0;
            this.txbxDdsFTW.Tag = "MaxValue";
            this.txbxDdsFTW.Text = "0";
            this.txbxDdsFTW.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.txbxDdsFTW.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txbxDdsParameters_KeyPress);
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(77, 26);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(34, 13);
            this.label9.TabIndex = 1;
            this.label9.Text = "FTW:";
            // 
            // FmDdsConfiguration
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(372, 331);
            this.Controls.Add(this.tbCtrlDds);
            this.Controls.Add(this.btClose);
            this.Controls.Add(this.btApply);
            this.Name = "FmDdsConfiguration";
            this.Text = "DDS configuration";
            this.grbxChannelOut3.ResumeLayout(false);
            this.grbxChannelOut3.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.tbCtrlDds.ResumeLayout(false);
            this.tbpgRegisters.ResumeLayout(false);
            this.tbpgControls.ResumeLayout(false);
            this.grbxChannelOut2.ResumeLayout(false);
            this.grbxChannelOut2.PerformLayout();
            this.grbxChannelOut1.ResumeLayout(false);
            this.grbxChannelOut1.PerformLayout();
            this.tbpgSettings.ResumeLayout(false);
            this.tbpgSettings.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btApply;
        private System.Windows.Forms.Button btClose;
        private System.Windows.Forms.GroupBox grbxChannelOut3;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox txbxDdsPhaseOut3;
        private System.Windows.Forms.CheckBox chbxOUT3;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox txbxDdsPhaseOut4;
        private System.Windows.Forms.CheckBox chbxOUT4;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox txbxDdsGainOut3;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox txbxDdsGainOut4;
        private System.Windows.Forms.TabControl tbCtrlDds;
        private System.Windows.Forms.TabPage tbpgControls;
        private System.Windows.Forms.GroupBox grbxChannelOut2;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox txbxDdsGainOut2;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txbxDdsPhaseOut2;
        private System.Windows.Forms.CheckBox chbxOUT2;
        private System.Windows.Forms.GroupBox grbxChannelOut1;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox txbxDdsGainOut1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txbxDdsPhaseOut1;
        private System.Windows.Forms.CheckBox chbxOUT1;
        private System.Windows.Forms.TabPage tbpgRegisters;
        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.TabPage tbpgSettings;
        private System.Windows.Forms.Label lbDdsMaxValue;
        private System.Windows.Forms.TextBox txbxDdsMaxValue;
        private System.Windows.Forms.Label lbDdsPatternPeriod;
        private System.Windows.Forms.TextBox txbxDdsPatternPeriod;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox txbxDdsFTW;
    }
}