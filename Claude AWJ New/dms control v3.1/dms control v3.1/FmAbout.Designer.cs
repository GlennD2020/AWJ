namespace dms_control_v3
{
    partial class FmAbout
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
            this.label1 = new System.Windows.Forms.Label();
            this.lbFirmwareVersion = new System.Windows.Forms.Label();
            this.lbHardwareVersion = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.btOK = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.lbSoftwareVersion = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label1.Location = new System.Drawing.Point(24, 45);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(123, 17);
            this.label1.TabIndex = 2;
            this.label1.Text = "Firmware version: ";
            // 
            // lbFirmwareVersion
            // 
            this.lbFirmwareVersion.AutoSize = true;
            this.lbFirmwareVersion.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.lbFirmwareVersion.Location = new System.Drawing.Point(152, 45);
            this.lbFirmwareVersion.Name = "lbFirmwareVersion";
            this.lbFirmwareVersion.Size = new System.Drawing.Size(36, 17);
            this.lbFirmwareVersion.TabIndex = 3;
            this.lbFirmwareVersion.Text = "0.00";
            // 
            // lbHardwareVersion
            // 
            this.lbHardwareVersion.AutoSize = true;
            this.lbHardwareVersion.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.lbHardwareVersion.Location = new System.Drawing.Point(153, 75);
            this.lbHardwareVersion.Name = "lbHardwareVersion";
            this.lbHardwareVersion.Size = new System.Drawing.Size(16, 17);
            this.lbHardwareVersion.TabIndex = 5;
            this.lbHardwareVersion.Text = "0";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label4.Location = new System.Drawing.Point(22, 75);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(127, 17);
            this.label4.TabIndex = 4;
            this.label4.Text = "Hardware version: ";
            // 
            // btOK
            // 
            this.btOK.Location = new System.Drawing.Point(182, 114);
            this.btOK.Name = "btOK";
            this.btOK.Size = new System.Drawing.Size(75, 25);
            this.btOK.TabIndex = 6;
            this.btOK.Text = "OK";
            this.btOK.UseVisualStyleBackColor = true;
            this.btOK.Click += new System.EventHandler(this.btOK_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label2.Location = new System.Drawing.Point(24, 15);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(121, 17);
            this.label2.TabIndex = 7;
            this.label2.Text = "Software version: ";
            // 
            // lbSoftwareVersion
            // 
            this.lbSoftwareVersion.AutoSize = true;
            this.lbSoftwareVersion.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.lbSoftwareVersion.Location = new System.Drawing.Point(151, 15);
            this.lbSoftwareVersion.Name = "lbSoftwareVersion";
            this.lbSoftwareVersion.Size = new System.Drawing.Size(36, 17);
            this.lbSoftwareVersion.TabIndex = 8;
            this.lbSoftwareVersion.Text = "0.00";
            // 
            // FmAbout
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(269, 151);
            this.Controls.Add(this.lbSoftwareVersion);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.btOK);
            this.Controls.Add(this.lbHardwareVersion);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.lbFirmwareVersion);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FmAbout";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "FmAbout";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label lbFirmwareVersion;
        private System.Windows.Forms.Label lbHardwareVersion;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btOK;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label lbSoftwareVersion;
    }
}