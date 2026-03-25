#nullable enable

using TeacherClient.Models;

namespace TeacherClient;

public partial class SettingsDialog : Form
{
    public SettingsDialog()
    {
        InitializeComponent();
    }

    public SettingsDialog(ClientSettings settings)
        : this()
    {
        sharedSecretTextBox.Text = settings.SharedSecret;
    }

    public ClientSettings ToSettings()
        => new(sharedSecretTextBox.Text.Trim());
}
