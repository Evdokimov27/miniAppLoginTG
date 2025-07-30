using System.Windows.Forms;
using System.Drawing;

public static class Prompt
{
	public static string ShowDialog(string text, string caption)
	{
		Form prompt = new Form()
		{
			Width = 400,
			Height = 150,
			FormBorderStyle = FormBorderStyle.FixedDialog,
			Text = caption,
			StartPosition = FormStartPosition.CenterScreen
		};

		Label textLabel = new Label() { Left = 20, Top = 20, Text = text, Width = 340 };
		TextBox textBox = new TextBox() { Left = 20, Top = 50, Width = 340 };
		Button confirmation = new Button() { Text = "OK", Left = 270, Width = 90, Top = 80 };
		confirmation.DialogResult = DialogResult.OK;

		prompt.Controls.Add(textLabel);
		prompt.Controls.Add(textBox);
		prompt.Controls.Add(confirmation);
		prompt.AcceptButton = confirmation;

		return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : null;
	}
}
