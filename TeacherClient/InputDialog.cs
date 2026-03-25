#nullable enable

namespace TeacherClient;

public partial class InputDialog : Form
{
    public InputDialog()
    {
        InitializeComponent();
    }

    public InputDialog(string title, string prompt, string defaultValue = "")
        : this()
    {
        Text = title;
        promptLabel.Text = prompt;
        valueTextBox.Text = defaultValue;
    }

    public string Value => valueTextBox.Text.Trim();
}
