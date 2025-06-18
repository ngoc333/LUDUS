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
            this.btnConnect = new System.Windows.Forms.Button();
            this.btnOpenApp = new System.Windows.Forms.Button();
            this.btnCloseApp = new System.Windows.Forms.Button();
            this.btnCapture = new System.Windows.Forms.Button();
            this.btnCrop = new System.Windows.Forms.Button();
            this.btnAnalyzeBattle = new System.Windows.Forms.Button();
            this.richTextBoxLog = new System.Windows.Forms.RichTextBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.btnSaveTemplate = new System.Windows.Forms.Button();
            this.txtHeroName = new System.Windows.Forms.TextBox();
            this.cmbCells = new System.Windows.Forms.ComboBox();
            this.cmbDevices = new System.Windows.Forms.ComboBox();
            this.flowLayoutPanel1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.btnConnect);
            this.flowLayoutPanel1.Controls.Add(this.btnOpenApp);
            this.flowLayoutPanel1.Controls.Add(this.btnCloseApp);
            this.flowLayoutPanel1.Controls.Add(this.btnCapture);
            this.flowLayoutPanel1.Controls.Add(this.btnCrop);
            this.flowLayoutPanel1.Controls.Add(this.btnAnalyzeBattle);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(825, 51);
            this.flowLayoutPanel1.TabIndex = 0;
            // 
            // btnConnect
            // 
            this.btnConnect.Location = new System.Drawing.Point(3, 3);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(75, 23);
            this.btnConnect.TabIndex = 0;
            this.btnConnect.Text = "Connect";
            this.btnConnect.UseVisualStyleBackColor = true;
            // 
            // btnOpenApp
            // 
            this.btnOpenApp.Location = new System.Drawing.Point(84, 3);
            this.btnOpenApp.Name = "btnOpenApp";
            this.btnOpenApp.Size = new System.Drawing.Size(75, 23);
            this.btnOpenApp.TabIndex = 2;
            this.btnOpenApp.Text = "Open App";
            this.btnOpenApp.UseVisualStyleBackColor = true;
            // 
            // btnCloseApp
            // 
            this.btnCloseApp.Location = new System.Drawing.Point(165, 3);
            this.btnCloseApp.Name = "btnCloseApp";
            this.btnCloseApp.Size = new System.Drawing.Size(75, 23);
            this.btnCloseApp.TabIndex = 3;
            this.btnCloseApp.Text = "Close App";
            this.btnCloseApp.UseVisualStyleBackColor = true;
            // 
            // btnCapture
            // 
            this.btnCapture.Location = new System.Drawing.Point(244, 1);
            this.btnCapture.Margin = new System.Windows.Forms.Padding(1);
            this.btnCapture.Name = "btnCapture";
            this.btnCapture.Size = new System.Drawing.Size(75, 23);
            this.btnCapture.TabIndex = 7;
            this.btnCapture.Text = "Capture";
            this.btnCapture.UseVisualStyleBackColor = true;
            // 
            // btnCrop
            // 
            this.btnCrop.Location = new System.Drawing.Point(321, 1);
            this.btnCrop.Margin = new System.Windows.Forms.Padding(1);
            this.btnCrop.Name = "btnCrop";
            this.btnCrop.Size = new System.Drawing.Size(75, 23);
            this.btnCrop.TabIndex = 10;
            this.btnCrop.Text = "Crop";
            this.btnCrop.UseVisualStyleBackColor = true;
            // 
            // btnAnalyzeBattle
            // 
            this.btnAnalyzeBattle.Location = new System.Drawing.Point(400, 3);
            this.btnAnalyzeBattle.Name = "btnAnalyzeBattle";
            this.btnAnalyzeBattle.Size = new System.Drawing.Size(89, 23);
            this.btnAnalyzeBattle.TabIndex = 5;
            this.btnAnalyzeBattle.Text = "Analyze Battle";
            this.btnAnalyzeBattle.UseVisualStyleBackColor = true;
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
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.btnSaveTemplate);
            this.panel1.Controls.Add(this.txtHeroName);
            this.panel1.Controls.Add(this.cmbCells);
            this.panel1.Controls.Add(this.cmbDevices);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(446, 51);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(379, 437);
            this.panel1.TabIndex = 5;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(34, 132);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(61, 13);
            this.label2.TabIndex = 16;
            this.label2.Text = "Hero Name";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(34, 105);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(24, 13);
            this.label1.TabIndex = 15;
            this.label1.Text = "Cell";
            // 
            // btnSaveTemplate
            // 
            this.btnSaveTemplate.Location = new System.Drawing.Point(242, 127);
            this.btnSaveTemplate.Name = "btnSaveTemplate";
            this.btnSaveTemplate.Size = new System.Drawing.Size(89, 23);
            this.btnSaveTemplate.TabIndex = 14;
            this.btnSaveTemplate.Text = "Save Template";
            this.btnSaveTemplate.UseVisualStyleBackColor = true;
            // 
            // txtHeroName
            // 
            this.txtHeroName.Location = new System.Drawing.Point(115, 129);
            this.txtHeroName.Name = "txtHeroName";
            this.txtHeroName.Size = new System.Drawing.Size(121, 20);
            this.txtHeroName.TabIndex = 13;
            // 
            // cmbCells
            // 
            this.cmbCells.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCells.FormattingEnabled = true;
            this.cmbCells.Location = new System.Drawing.Point(115, 102);
            this.cmbCells.Name = "cmbCells";
            this.cmbCells.Size = new System.Drawing.Size(121, 21);
            this.cmbCells.TabIndex = 12;
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
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.RichTextBox richTextBoxLog;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Button btnCloseApp;
        private System.Windows.Forms.Button btnOpenApp;
        private System.Windows.Forms.Button btnCapture;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Button btnCrop;
        private System.Windows.Forms.Button btnAnalyzeBattle;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnSaveTemplate;
        private System.Windows.Forms.TextBox txtHeroName;
        private System.Windows.Forms.ComboBox cmbCells;
        private System.Windows.Forms.ComboBox cmbDevices;
    }
}

