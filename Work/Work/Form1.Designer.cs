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
			textBox1 = new TextBox();
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
			listBoxCookies.ItemHeight = 15;
			listBoxCookies.Location = new Point(25, 35);
			listBoxCookies.Name = "listBoxCookies";
			listBoxCookies.Size = new Size(211, 289);
			listBoxCookies.TabIndex = 0;
			// 
			// buttonOpen
			// 
			buttonOpen.Location = new Point(25, 342);
			buttonOpen.Name = "buttonOpen";
			buttonOpen.Size = new Size(77, 48);
			buttonOpen.TabIndex = 1;
			buttonOpen.Text = "Открыть браузер";
			buttonOpen.UseVisualStyleBackColor = true;
			buttonOpen.Click += buttonOpen_Click;
			// 
			// button2
			// 
			button2.Location = new Point(121, 342);
			button2.Name = "button2";
			button2.Size = new Size(77, 48);
			button2.TabIndex = 2;
			button2.Text = "Сохранить данные";
			button2.UseVisualStyleBackColor = true;
			button2.Click += buttonSaveCookies_Click;
			// 
			// button1
			// 
			button1.Location = new Point(484, 356);
			button1.Name = "button1";
			button1.Size = new Size(79, 81);
			button1.TabIndex = 3;
			button1.Text = "Выбрать файл с сайтами";
			button1.UseVisualStyleBackColor = true;
			button1.Click += buttonSend_Click;
			// 
			// proxyBox
			// 
			proxyBox.Location = new Point(25, 414);
			proxyBox.Name = "proxyBox";
			proxyBox.Size = new Size(173, 23);
			proxyBox.TabIndex = 4;
			proxyBox.Text = "socks5://user:pass@host:port";
			// 
			// textBox1
			// 
			textBox1.Location = new Point(224, 356);
			textBox1.Multiline = true;
			textBox1.Name = "textBox1";
			textBox1.Size = new Size(254, 81);
			textBox1.TabIndex = 5;
			// 
			// label1
			// 
			label1.AutoSize = true;
			label1.Location = new Point(372, 308);
			label1.Name = "label1";
			label1.Size = new Size(49, 15);
			label1.TabIndex = 6;
			label1.Text = "Ссылка";
			// 
			// label2
			// 
			label2.AutoSize = true;
			label2.Location = new Point(372, 279);
			label2.Name = "label2";
			label2.Size = new Size(36, 15);
			label2.TabIndex = 7;
			label2.Text = "Текст";
			// 
			// smsBox
			// 
			smsBox.FormattingEnabled = true;
			smsBox.ItemHeight = 15;
			smsBox.Location = new Point(261, 39);
			smsBox.Name = "smsBox";
			smsBox.Size = new Size(302, 289);
			smsBox.TabIndex = 8;
			// 
			// label3
			// 
			label3.AutoSize = true;
			label3.Location = new Point(25, 393);
			label3.Name = "label3";
			label3.Size = new Size(49, 15);
			label3.TabIndex = 9;
			label3.Text = "Прокси";
			// 
			// label4
			// 
			label4.AutoSize = true;
			label4.Location = new Point(224, 338);
			label4.Name = "label4";
			label4.Size = new Size(73, 15);
			label4.TabIndex = 10;
			label4.Text = "Сообщение";
			// 
			// label5
			// 
			label5.AutoSize = true;
			label5.Location = new Point(261, 21);
			label5.Name = "label5";
			label5.Size = new Size(34, 15);
			label5.TabIndex = 11;
			label5.Text = "Логи";
			// 
			// label6
			// 
			label6.AutoSize = true;
			label6.Location = new Point(25, 17);
			label6.Name = "label6";
			label6.Size = new Size(33, 15);
			label6.TabIndex = 12;
			label6.Text = "Куки";
			// 
			// Form1
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(583, 450);
			Controls.Add(label6);
			Controls.Add(label5);
			Controls.Add(label4);
			Controls.Add(label3);
			Controls.Add(smsBox);
			Controls.Add(label2);
			Controls.Add(label1);
			Controls.Add(textBox1);
			Controls.Add(proxyBox);
			Controls.Add(button1);
			Controls.Add(button2);
			Controls.Add(buttonOpen);
			Controls.Add(listBoxCookies);
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
		private TextBox textBox1;
		private Label label1;
		private Label label2;
		private ListBox smsBox;
		private Label label3;
		private Label label4;
		private Label label5;
		private Label label6;
	}
}
