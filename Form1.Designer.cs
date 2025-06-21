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
            this.btnAnalyzeBattle = new System.Windows.Forms.Button();
            this.btnScreenDetect = new System.Windows.Forms.Button();
            this.richTextBoxLog = new System.Windows.Forms.RichTextBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.cmbDevices = new System.Windows.Forms.ComboBox();
            this.btnStart = new System.Windows.Forms.Button();
            this.flowLayoutPanel1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.btnOpenApp);
            this.flowLayoutPanel1.Controls.Add(this.btnCloseApp);
            this.flowLayoutPanel1.Controls.Add(this.btnCapture);
            this.flowLayoutPanel1.Controls.Add(this.btnAnalyzeBattle);
            this.flowLayoutPanel1.Controls.Add(this.btnScreenDetect);
            this.flowLayoutPanel1.Controls.Add(this.btnStart);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(825, 51);
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
            // btnAnalyzeBattle
            // 
            this.btnAnalyzeBattle.Location = new System.Drawing.Point(246, 3);
            this.btnAnalyzeBattle.Name = "btnAnalyzeBattle";
            this.btnAnalyzeBattle.Size = new System.Drawing.Size(89, 23);
            this.btnAnalyzeBattle.TabIndex = 5;
            this.btnAnalyzeBattle.Text = "Analyze Battle";
            this.btnAnalyzeBattle.UseVisualStyleBackColor = true;
            // 
            // btnScreenDetect
            // 
            this.btnScreenDetect.Location = new System.Drawing.Point(341, 3);
            this.btnScreenDetect.Name = "btnScreenDetect";
            this.btnScreenDetect.Size = new System.Drawing.Size(89, 23);
            this.btnScreenDetect.TabIndex = 8;
            this.btnScreenDetect.Text = "Screen Detect";
            this.btnScreenDetect.UseVisualStyleBackColor = true;
            // 
            // richTextBoxLog
            // 
            this.richTextBoxLog.Dock = System.Windows.Forms.DockStyle.Left;
            this.richTextBoxLog.Location = new System.Drawing.Point(0, 51);
            this.richTextBoxLog.Name = "richTextBoxLog";
            this.richTextBoxLog.Size = new System.Drawing.Size(446, 437);
            this.richTextBoxLog.TabIndex = 4;
            this.richTextBoxLog.Text = "";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.cmbDevices);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(446, 51);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(379, 437);
            this.panel1.TabIndex = 5;
            // 
            // cmbDevices
            // 
            this.cmbDevices.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbDevices.FormattingEnabled = true;
            this.cmbDevices.Location = new System.Drawing.Point(210, 21);
            this.cmbDevices.Name = "cmbDevices";
            this.cmbDevices.Size = new System.Drawing.Size(121, 21);
            this.cmbDevices.TabIndex = 11;
            // 
            // btnStart
            // 
            this.btnStart.Location = new System.Drawing.Point(436, 3);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(75, 23);
            this.btnStart.TabIndex = 9;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(825, 488);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.richTextBoxLog);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Name = "Form1";
            this.Text = "LUDUS Auto";
            this.flowLayoutPanel1.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.RichTextBox richTextBoxLog;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Button btnCloseApp;
        private System.Windows.Forms.Button btnOpenApp;
        private System.Windows.Forms.Button btnCapture;
        private System.Windows.Forms.Button btnAnalyzeBattle;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ComboBox cmbDevices;
        private System.Windows.Forms.Button btnScreenDetect;
        private System.Windows.Forms.Button btnStart;
    }
}

