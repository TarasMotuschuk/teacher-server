namespace StudentAgent.UI;

public sealed class PasswordPromptForm : Form
{
    private readonly TextBox _passwordTextBox;

    public PasswordPromptForm()
    {
        Text = "Enter password";
        Width = 360;
        Height = 170;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var label = new Label
        {
            Left = 12,
            Top = 18,
            Width = 320,
            Text = "Enter the StudentAgent admin password:"
        };

        _passwordTextBox = new TextBox
        {
            Left = 12,
            Top = 48,
            Width = 320,
            UseSystemPasswordChar = true
        };

        var okButton = new Button
        {
            Text = "OK",
            Left = 176,
            Top = 84,
            Width = 75,
            Height = 32,
            DialogResult = DialogResult.OK
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            Left = 257,
            Top = 84,
            Width = 75,
            Height = 32,
            DialogResult = DialogResult.Cancel
        };

        Controls.Add(label);
        Controls.Add(_passwordTextBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    public string Password => _passwordTextBox.Text.Trim();
}
