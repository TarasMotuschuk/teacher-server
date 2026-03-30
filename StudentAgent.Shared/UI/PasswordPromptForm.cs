using StudentAgent.UI.Localization;

namespace StudentAgent.UI;

public partial class PasswordPromptForm : Form
{
    public PasswordPromptForm()
    {
        InitializeComponent();
        Text = StudentAgentText.EnterPasswordTitle;
        passwordLabel.Text = StudentAgentText.EnterPasswordPrompt;
        okButton.Text = StudentAgentText.Ok;
        cancelButton.Text = StudentAgentText.Cancel;
    }

    public string Password => passwordTextBox.Text.Trim();
}
