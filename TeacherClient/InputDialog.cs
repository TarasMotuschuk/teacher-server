namespace TeacherClient;

public sealed class InputDialog : Form
{
    private readonly TextBox _textBox;

    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        Text = title;
        Width = 420;
        Height = 150;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        var label = new Label
        {
            Left = 12,
            Top = 14,
            Width = 380,
            Text = prompt
        };

        _textBox = new TextBox
        {
            Left = 12,
            Top = 40,
            Width = 380,
            Text = defaultValue
        };

        var okButton = new Button
        {
            Text = "OK",
            Left = 236,
            Width = 75,
            Top = 72,
            DialogResult = DialogResult.OK
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            Left = 317,
            Width = 75,
            Top = 72,
            DialogResult = DialogResult.Cancel
        };

        Controls.Add(label);
        Controls.Add(_textBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    public string Value => _textBox.Text.Trim();
}
