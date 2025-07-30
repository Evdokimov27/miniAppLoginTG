namespace Work
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            listBoxCookies = new ListBox();
            buttonOpen = new Button();
            button2 = new Button();
            button1 = new Button();
            proxyBox = new TextBox();
            messageBox = new TextBox();
            label1 = new Label();
            label2 = new Label();
            smsBox = new ListBox();
            label3 = new Label();
            label4 = new Label();
            label5 = new Label();
            label6 = new Label();
            SuspendLayout();
            // 
            // listBoxCookies
            // 
            listBoxCookies.FormattingEnabled = true;
            listBoxCookies.ItemHeight = 30;
            listBoxCookies.Location = new Point(43, 70);
            listBoxCookies.Margin = new Padding(5, 6, 5, 6);
            listBoxCookies.Name = "listBoxCookies";
            listBoxCookies.Size = new Size(359, 574);
            listBoxCookies.TabIndex = 0;
            // 
            // buttonOpen
            // 
            buttonOpen.Location = new Point(43, 684);
            buttonOpen.Margin = new Padding(5, 6, 5, 6);
            buttonOpen.Name = "buttonOpen";
            buttonOpen.Size = new Size(132, 96);
            buttonOpen.TabIndex = 1;
            buttonOpen.Text = "Открыть браузер";
            buttonOpen.UseVisualStyleBackColor = true;
            buttonOpen.Click += buttonOpen_Click;
            // 
            // button2
            // 
            button2.Location = new Point(207, 684);
            button2.Margin = new Padding(5, 6, 5, 6);
            button2.Name = "button2";
            button2.Size = new Size(132, 96);
            button2.TabIndex = 2;
            button2.Text = "Сохранить данные";
            button2.UseVisualStyleBackColor = true;
            button2.Click += buttonSaveCookies_Click;
            // 
            // button1
            // 
            button1.Location = new Point(830, 712);
            button1.Margin = new Padding(5, 6, 5, 6);
            button1.Name = "button1";
            button1.Size = new Size(135, 162);
            button1.TabIndex = 3;
            button1.Text = "Выбрать файл с сайтами";
            button1.UseVisualStyleBackColor = true;
            button1.Click += buttonSend_Click;
            // 
            // proxyBox
            // 
            proxyBox.Location = new Point(43, 828);
            proxyBox.Margin = new Padding(5, 6, 5, 6);
            proxyBox.Name = "proxyBox";
            proxyBox.Size = new Size(294, 35);
            proxyBox.TabIndex = 4;
            proxyBox.Text = "socks5://91.242.238.193:34660";
            // 
            // messageBox
            // 
            messageBox.Location = new Point(384, 712);
            messageBox.Margin = new Padding(5, 6, 5, 6);
            messageBox.Multiline = true;
            messageBox.Name = "messageBox";
            messageBox.Size = new Size(433, 158);
            messageBox.TabIndex = 5;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(638, 616);
            label1.Margin = new Padding(5, 0, 5, 0);
            label1.Name = "label1";
            label1.Size = new Size(83, 30);
            label1.TabIndex = 6;
            label1.Text = "Ссылка";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(638, 558);
            label2.Margin = new Padding(5, 0, 5, 0);
            label2.Name = "label2";
            label2.Size = new Size(64, 30);
            label2.TabIndex = 7;
            label2.Text = "Текст";
            // 
            // smsBox
            // 
            smsBox.FormattingEnabled = true;
            smsBox.ItemHeight = 30;
            smsBox.Location = new Point(447, 78);
            smsBox.Margin = new Padding(5, 6, 5, 6);
            smsBox.Name = "smsBox";
            smsBox.Size = new Size(515, 574);
            smsBox.TabIndex = 8;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(43, 786);
            label3.Margin = new Padding(5, 0, 5, 0);
            label3.Name = "label3";
            label3.Size = new Size(84, 30);
            label3.TabIndex = 9;
            label3.Text = "Прокси";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(384, 676);
            label4.Margin = new Padding(5, 0, 5, 0);
            label4.Name = "label4";
            label4.Size = new Size(125, 30);
            label4.TabIndex = 10;
            label4.Text = "Сообщение";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(447, 42);
            label5.Margin = new Padding(5, 0, 5, 0);
            label5.Name = "label5";
            label5.Size = new Size(59, 30);
            label5.TabIndex = 11;
            label5.Text = "Логи";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(43, 34);
            label6.Margin = new Padding(5, 0, 5, 0);
            label6.Name = "label6";
            label6.Size = new Size(57, 30);
            label6.TabIndex = 12;
            label6.Text = "Куки";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(12F, 30F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(999, 900);
            Controls.Add(label6);
            Controls.Add(label5);
            Controls.Add(label4);
            Controls.Add(label3);
            Controls.Add(smsBox);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(messageBox);
            Controls.Add(proxyBox);
            Controls.Add(button1);
            Controls.Add(button2);
            Controls.Add(buttonOpen);
            Controls.Add(listBoxCookies);
            Margin = new Padding(5, 6, 5, 6);
            Name = "Form1";
            Text = "Form1";
            Load += Form1_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ListBox listBoxCookies;
		private Button buttonOpen;
		private Button button2;
		private Button button1;
		private TextBox proxyBox;
		private TextBox messageBox;
		private Label label1;
		private Label label2;
		private ListBox smsBox;
		private Label label3;
		private Label label4;
		private Label label5;
		private Label label6;
	}
}
