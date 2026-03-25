namespace StudentAgent.UI;

public partial class PasswordPromptForm : Form
{
    public PasswordPromptForm()
    {
        InitializeComponent();
    }

    public string Password => passwordTextBox.Text.Trim();
}
