#nullable enable

using TeacherClient.Localization;

namespace TeacherClient;

public partial class InputDialog : Form
{
    public InputDialog()
    {
        InitializeComponent();
        Text = TeacherClientText.InputTitle;
        promptLabel.Text = TeacherClientText.Prompt;
        okButton.Text = TeacherClientText.Ok;
        cancelButton.Text = TeacherClientText.Cancel;
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
