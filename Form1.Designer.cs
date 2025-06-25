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
            this.btnCloseApp = new System.Windows.Forms.Button();
            this.btnCapture = new System.Windows.Forms.Button();
            this.btnStart = new System.Windows.Forms.Button();
            this.richTextBoxLog = new System.Windows.Forms.RichTextBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.flowLayoutPanel2 = new System.Windows.Forms.FlowLayoutPanel();
            this.lblWin = new System.Windows.Forms.Label();
            this.lblLose = new System.Windows.Forms.Label();
            this.cmbDevices = new System.Windows.Forms.ComboBox();
            this.richTextBoxResult = new System.Windows.Forms.RichTextBox();
            this.flowLayoutPanel1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.flowLayoutPanel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.btnOpenApp);
            this.flowLayoutPanel1.Controls.Add(this.btnCloseApp);
            this.flowLayoutPanel1.Controls.Add(this.btnCapture);
            this.flowLayoutPanel1.Controls.Add(this.btnStart);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(906, 51);
            this.flowLayoutPanel1.TabIndex = 0;
            // 
            // btnOpenApp
            // 
            this.btnOpenApp.Location = new System.Drawing.Point(3, 3);
            this.btnOpenApp.Name = "btnOpenApp";
            this.btnOpenApp.Size = new System.Drawing.Size(75, 23);
            this.btnOpenApp.TabIndex = 2;
            this.btnOpenApp.Text = "Open App";
            this.btnOpenApp.UseVisualStyleBackColor = true;
            // 
            // btnCloseApp
            // 
            this.btnCloseApp.Location = new System.Drawing.Point(84, 3);
            this.btnCloseApp.Name = "btnCloseApp";
            this.btnCloseApp.Size = new System.Drawing.Size(75, 23);
            this.btnCloseApp.TabIndex = 3;
            this.btnCloseApp.Text = "Close App";
            this.btnCloseApp.UseVisualStyleBackColor = true;
            // 
            // btnCapture
            // 
            this.btnCapture.Location = new System.Drawing.Point(165, 3);
            this.btnCapture.Name = "btnCapture";
            this.btnCapture.Size = new System.Drawing.Size(75, 23);
            this.btnCapture.TabIndex = 7;
            this.btnCapture.Text = "Capture";
            this.btnCapture.UseVisualStyleBackColor = true;
            // 
            // btnStart
            // 
            this.btnStart.Location = new System.Drawing.Point(246, 3);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(75, 23);
            this.btnStart.TabIndex = 9;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            // 
            // richTextBoxLog
            // 
            this.richTextBoxLog.Dock = System.Windows.Forms.DockStyle.Left;
            this.richTextBoxLog.Location = new System.Drawing.Point(0, 51);
            this.richTextBoxLog.Name = "richTextBoxLog";
            this.richTextBoxLog.Size = new System.Drawing.Size(488, 445);
            this.richTextBoxLog.TabIndex = 4;
            this.richTextBoxLog.Text = "";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.flowLayoutPanel2);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(488, 51);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(163, 445);
            this.panel1.TabIndex = 5;
            // 
            // flowLayoutPanel2
            // 
            this.flowLayoutPanel2.Controls.Add(this.lblWin);
            this.flowLayoutPanel2.Controls.Add(this.lblLose);
            this.flowLayoutPanel2.Controls.Add(this.cmbDevices);
            this.flowLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanel2.Location = new System.Drawing.Point(0, 0);
            this.flowLayoutPanel2.Name = "flowLayoutPanel2";
            this.flowLayoutPanel2.Size = new System.Drawing.Size(163, 445);
            this.flowLayoutPanel2.TabIndex = 0;
            // 
            // lblWin
            // 
            this.lblWin.AutoSize = true;
            this.lblWin.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblWin.Location = new System.Drawing.Point(3, 3);
            this.lblWin.Margin = new System.Windows.Forms.Padding(3);
            this.lblWin.Name = "lblWin";
            this.lblWin.Size = new System.Drawing.Size(55, 15);
            this.lblWin.TabIndex = 12;
            this.lblWin.Text = "Thắng: 0";
            this.lblWin.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblLose
            // 
            this.lblLose.AutoSize = true;
            this.lblLose.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLose.Location = new System.Drawing.Point(64, 3);
            this.lblLose.Margin = new System.Windows.Forms.Padding(3);
            this.lblLose.Name = "lblLose";
            this.lblLose.Size = new System.Drawing.Size(48, 15);
            this.lblLose.TabIndex = 13;
            this.lblLose.Text = "Thua: 0";
            // 
            // cmbDevices
            // 
            this.cmbDevices.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbDevices.FormattingEnabled = true;
            this.cmbDevices.Location = new System.Drawing.Point(3, 24);
            this.cmbDevices.Name = "cmbDevices";
            this.cmbDevices.Size = new System.Drawing.Size(154, 21);
            this.cmbDevices.TabIndex = 11;
            // 
            // richTextBoxResult
            // 
            this.richTextBoxResult.Dock = System.Windows.Forms.DockStyle.Right;
            this.richTextBoxResult.Location = new System.Drawing.Point(651, 51);
            this.richTextBoxResult.Name = "richTextBoxResult";
            this.richTextBoxResult.Size = new System.Drawing.Size(255, 445);
            this.richTextBoxResult.TabIndex = 14;
            this.richTextBoxResult.Text = "";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(906, 496);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.richTextBoxResult);
            this.Controls.Add(this.richTextBoxLog);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Name = "Form1";
            this.Text = "LUDUS Auto";
            this.flowLayoutPanel1.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.flowLayoutPanel2.ResumeLayout(false);
            this.flowLayoutPanel2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.RichTextBox richTextBoxLog;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Button btnCloseApp;
        private System.Windows.Forms.Button btnOpenApp;
        private System.Windows.Forms.Button btnCapture;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ComboBox cmbDevices;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Label lblWin;
        private System.Windows.Forms.Label lblLose;
        private System.Windows.Forms.RichTextBox richTextBoxResult;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel2;
    }
}

