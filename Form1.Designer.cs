namespace LUDUS {
    partial class Form1 {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.btnOpenApp = new System.Windows.Forms.Button();
            this.btnOpenClose = new System.Windows.Forms.Button();
            this.btnCapture = new System.Windows.Forms.Button();
            this.btnStartAstra = new System.Windows.Forms.Button();
            this.btnStartPvp = new System.Windows.Forms.Button();
            this.cmbDevices = new System.Windows.Forms.ComboBox();
            this.lblLoseCount = new System.Windows.Forms.Label();
            this.numLoseCount = new System.Windows.Forms.NumericUpDown();
            this.lblWin = new System.Windows.Forms.Label();
            this.lblLose = new System.Windows.Forms.Label();
            this.richTextBoxResult = new System.Windows.Forms.RichTextBox();
            this.richTextBoxLog = new System.Windows.Forms.RichTextBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.label1 = new System.Windows.Forms.Label();
            this.numWin = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.numLose = new System.Windows.Forms.NumericUpDown();
            this.flowLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numLoseCount)).BeginInit();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numWin)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLose)).BeginInit();
            this.SuspendLayout();
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.btnOpenApp);
            this.flowLayoutPanel1.Controls.Add(this.btnOpenClose);
            this.flowLayoutPanel1.Controls.Add(this.btnCapture);
            this.flowLayoutPanel1.Controls.Add(this.btnStartAstra);
            this.flowLayoutPanel1.Controls.Add(this.btnStartPvp);
            this.flowLayoutPanel1.Controls.Add(this.cmbDevices);
            this.flowLayoutPanel1.Controls.Add(this.lblLoseCount);
            this.flowLayoutPanel1.Controls.Add(this.numLoseCount);
            this.flowLayoutPanel1.Controls.Add(this.lblWin);
            this.flowLayoutPanel1.Controls.Add(this.lblLose);
            this.flowLayoutPanel1.Controls.Add(this.panel1);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(802, 94);
            this.flowLayoutPanel1.TabIndex = 0;
            // 
            // btnOpenApp
            // 
            this.btnOpenApp.Location = new System.Drawing.Point(3, 3);
            this.btnOpenApp.Name = "btnOpenApp";
            this.btnOpenApp.Size = new System.Drawing.Size(75, 42);
            this.btnOpenApp.TabIndex = 2;
            this.btnOpenApp.Text = "Open LDPlayer";
            this.btnOpenApp.UseVisualStyleBackColor = true;
            // 
            // btnOpenClose
            // 
            this.btnOpenClose.Location = new System.Drawing.Point(84, 3);
            this.btnOpenClose.Name = "btnOpenClose";
            this.btnOpenClose.Size = new System.Drawing.Size(75, 42);
            this.btnOpenClose.TabIndex = 17;
            this.btnOpenClose.Text = "Open App";
            this.btnOpenClose.UseVisualStyleBackColor = true;
            // 
            // btnCapture
            // 
            this.btnCapture.Location = new System.Drawing.Point(165, 3);
            this.btnCapture.Name = "btnCapture";
            this.btnCapture.Size = new System.Drawing.Size(75, 42);
            this.btnCapture.TabIndex = 7;
            this.btnCapture.Text = "Capture";
            this.btnCapture.UseVisualStyleBackColor = true;
            // 
            // btnStartAstra
            // 
            this.btnStartAstra.Location = new System.Drawing.Point(246, 3);
            this.btnStartAstra.Name = "btnStartAstra";
            this.btnStartAstra.Size = new System.Drawing.Size(84, 42);
            this.btnStartAstra.TabIndex = 9;
            this.btnStartAstra.Text = "Start Astra";
            this.btnStartAstra.UseVisualStyleBackColor = true;
            // 
            // btnStartPvp
            // 
            this.btnStartPvp.Location = new System.Drawing.Point(336, 3);
            this.btnStartPvp.Name = "btnStartPvp";
            this.btnStartPvp.Size = new System.Drawing.Size(75, 42);
            this.btnStartPvp.TabIndex = 3;
            this.btnStartPvp.Text = "Start PvP";
            this.btnStartPvp.UseVisualStyleBackColor = true;
            // 
            // cmbDevices
            // 
            this.cmbDevices.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbDevices.FormattingEnabled = true;
            this.cmbDevices.Location = new System.Drawing.Point(417, 3);
            this.cmbDevices.Name = "cmbDevices";
            this.cmbDevices.Size = new System.Drawing.Size(154, 21);
            this.cmbDevices.TabIndex = 11;
            // 
            // lblLoseCount
            // 
            this.lblLoseCount.AutoSize = true;
            this.lblLoseCount.Location = new System.Drawing.Point(577, 0);
            this.lblLoseCount.Name = "lblLoseCount";
            this.lblLoseCount.Size = new System.Drawing.Size(51, 26);
            this.lblLoseCount.TabIndex = 15;
            this.lblLoseCount.Text = "Số Thua \r\nCòn lại";
            // 
            // numLoseCount
            // 
            this.numLoseCount.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.numLoseCount.Location = new System.Drawing.Point(634, 3);
            this.numLoseCount.Name = "numLoseCount";
            this.numLoseCount.Size = new System.Drawing.Size(60, 26);
            this.numLoseCount.TabIndex = 16;
            // 
            // lblWin
            // 
            this.lblWin.AutoSize = true;
            this.lblWin.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblWin.Location = new System.Drawing.Point(700, 3);
            this.lblWin.Margin = new System.Windows.Forms.Padding(3);
            this.lblWin.Name = "lblWin";
            this.lblWin.Size = new System.Drawing.Size(42, 30);
            this.lblWin.TabIndex = 12;
            this.lblWin.Text = "Thắng\r\n0";
            this.lblWin.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblLose
            // 
            this.lblLose.AutoSize = true;
            this.lblLose.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLose.Location = new System.Drawing.Point(748, 3);
            this.lblLose.Margin = new System.Windows.Forms.Padding(3);
            this.lblLose.Name = "lblLose";
            this.lblLose.Size = new System.Drawing.Size(35, 30);
            this.lblLose.TabIndex = 13;
            this.lblLose.Text = "Thua\r\n0";
            this.lblLose.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // richTextBoxResult
            // 
            this.richTextBoxResult.Dock = System.Windows.Forms.DockStyle.Right;
            this.richTextBoxResult.Location = new System.Drawing.Point(379, 94);
            this.richTextBoxResult.Name = "richTextBoxResult";
            this.richTextBoxResult.Size = new System.Drawing.Size(423, 402);
            this.richTextBoxResult.TabIndex = 14;
            this.richTextBoxResult.Text = "";
            // 
            // richTextBoxLog
            // 
            this.richTextBoxLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.richTextBoxLog.Location = new System.Drawing.Point(0, 94);
            this.richTextBoxLog.Name = "richTextBoxLog";
            this.richTextBoxLog.Size = new System.Drawing.Size(379, 402);
            this.richTextBoxLog.TabIndex = 15;
            this.richTextBoxLog.Text = "";
            // 
            // panel1
            // 
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.numLose);
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.numWin);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Location = new System.Drawing.Point(3, 51);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(289, 37);
            this.panel1.TabIndex = 18;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 11);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(29, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Sau ";
            // 
            // numWin
            // 
            this.numWin.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.numWin.Location = new System.Drawing.Point(44, 5);
            this.numWin.Name = "numWin";
            this.numWin.Size = new System.Drawing.Size(60, 26);
            this.numWin.TabIndex = 17;
            this.numWin.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(110, 11);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(107, 13);
            this.label2.TabIndex = 18;
            this.label2.Text = "Trận Thắng Sẽ Thua";
            // 
            // numLose
            // 
            this.numLose.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.numLose.Location = new System.Drawing.Point(219, 5);
            this.numLose.Name = "numLose";
            this.numLose.Size = new System.Drawing.Size(60, 26);
            this.numLose.TabIndex = 19;
            this.numLose.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(802, 496);
            this.Controls.Add(this.richTextBoxLog);
            this.Controls.Add(this.richTextBoxResult);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Name = "Form1";
            this.Text = "LUDUS Auto";
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numLoseCount)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numWin)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLose)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Button btnStartPvp;
        private System.Windows.Forms.Button btnOpenApp;
        private System.Windows.Forms.Button btnCapture;
        private System.Windows.Forms.ComboBox cmbDevices;
        private System.Windows.Forms.Button btnStartAstra;
        private System.Windows.Forms.Label lblWin;
        private System.Windows.Forms.Label lblLose;
        private System.Windows.Forms.RichTextBox richTextBoxResult;
        private System.Windows.Forms.Label lblLoseCount;
        private System.Windows.Forms.NumericUpDown numLoseCount;
        private System.Windows.Forms.RichTextBox richTextBoxLog;
        private System.Windows.Forms.Button btnOpenClose;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown numLose;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown numWin;
    }
}

