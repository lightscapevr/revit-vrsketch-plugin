namespace VRSketch
{
    partial class Form1
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.closeBtn = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.sendPCBtn = new System.Windows.Forms.Button();
            this.sendQuestBtn = new System.Windows.Forms.Button();
            this.questIdBox = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.sendPCBtn);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Location = new System.Drawing.Point(36, 43);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(600, 427);
            this.panel1.TabIndex = 0;
            // 
            // panel2
            // 
            this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel2.Controls.Add(this.label4);
            this.panel2.Controls.Add(this.questIdBox);
            this.panel2.Controls.Add(this.sendQuestBtn);
            this.panel2.Controls.Add(this.label2);
            this.panel2.Location = new System.Drawing.Point(708, 43);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(600, 427);
            this.panel2.TabIndex = 1;
            // 
            // closeBtn
            // 
            this.closeBtn.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.closeBtn.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.closeBtn.Location = new System.Drawing.Point(1040, 538);
            this.closeBtn.Name = "closeBtn";
            this.closeBtn.Size = new System.Drawing.Size(268, 49);
            this.closeBtn.TabIndex = 9;
            this.closeBtn.Text = "Close";
            this.closeBtn.UseVisualStyleBackColor = true;
            this.closeBtn.Click += new System.EventHandler(this.closeBtn_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(33, 27);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(72, 25);
            this.label1.TabIndex = 0;
            this.label1.Text = "PC VR";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(34, 27);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(242, 25);
            this.label2.TabIndex = 1;
            this.label2.Text = "Quest/Pico standalone VR";
            // 
            // sendPCBtn
            // 
            this.sendPCBtn.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.sendPCBtn.Location = new System.Drawing.Point(74, 186);
            this.sendPCBtn.Name = "sendPCBtn";
            this.sendPCBtn.Size = new System.Drawing.Size(447, 121);
            this.sendPCBtn.TabIndex = 1;
            this.sendPCBtn.Text = "Send to VR on this PC";
            this.toolTip1.SetToolTip(this.sendPCBtn, resources.GetString("sendPCBtn.ToolTip"));
            this.sendPCBtn.UseVisualStyleBackColor = true;
            this.sendPCBtn.Click += new System.EventHandler(this.sendPCBtn_Click);
            // 
            // sendQuestBtn
            // 
            this.sendQuestBtn.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.sendQuestBtn.Location = new System.Drawing.Point(68, 186);
            this.sendQuestBtn.Name = "sendQuestBtn";
            this.sendQuestBtn.Size = new System.Drawing.Size(458, 121);
            this.sendQuestBtn.TabIndex = 5;
            this.sendQuestBtn.Text = "Send to VR on this standalone headset";
            this.toolTip1.SetToolTip(this.sendQuestBtn, resources.GetString("sendQuestBtn.ToolTip"));
            this.sendQuestBtn.UseVisualStyleBackColor = true;
            this.sendQuestBtn.Click += new System.EventHandler(this.sendQuestBtn_Click);
            // 
            // questIdBox
            // 
            this.questIdBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.questIdBox.Location = new System.Drawing.Point(290, 106);
            this.questIdBox.Name = "questIdBox";
            this.questIdBox.Size = new System.Drawing.Size(218, 39);
            this.questIdBox.TabIndex = 4;
            this.questIdBox.Text = "444444";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(140, 116);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(105, 25);
            this.label4.TabIndex = 5;
            this.label4.Text = "Quest ID #";
            // 
            // toolTip1
            // 
            this.toolTip1.AutoPopDelay = 500000;
            this.toolTip1.InitialDelay = 500;
            this.toolTip1.ReshowDelay = 100;
            // 
            // comboBox1
            // 
            this.comboBox1.DropDownHeight = 192;
            this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.IntegralHeight = false;
            this.comboBox1.Items.AddRange(new object[] {
            "Details 0/5",
            "Details 1/5",
            "Details 2/5",
            "Details 3/5 (default)",
            "Details 4/5",
            "Details 5/5"});
            this.comboBox1.Location = new System.Drawing.Point(439, 538);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(405, 32);
            this.comboBox1.TabIndex = 3;
            this.toolTip1.SetToolTip(this.comboBox1, resources.GetString("comboBox1.ToolTip"));
            this.comboBox1.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 24F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.closeBtn;
            this.ClientSize = new System.Drawing.Size(1356, 641);
            this.Controls.Add(this.comboBox1);
            this.Controls.Add(this.closeBtn);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Form1";
            this.ShowInTaskbar = false;
            this.Text = "VR Sketch";
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button closeBtn;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button sendPCBtn;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox questIdBox;
        private System.Windows.Forms.Button sendQuestBtn;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.ComboBox comboBox1;
    }
}