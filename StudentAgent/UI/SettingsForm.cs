using StudentAgent.Services;

namespace StudentAgent.UI;

public partial class SettingsForm : Form
{
    private AgentSettingsStore? _settingsStore;
    private AgentLogService? _logService;

    public SettingsForm()
    {
        InitializeComponent();
    }

    public SettingsForm(AgentSettingsStore settingsStore, AgentLogService logService)
        : this()
    {
        _settingsStore = settingsStore;
        _logService = logService;

        var settings = _settingsStore.Current;
        sharedSecretTextBox.Text = settings.SharedSecret;
    }

    private void clearLogsButton_Click(object? sender, EventArgs e)
    {
        if (_logService is null)
        {
            return;
        }

        if (MessageBox.Show(
                "Clear all StudentAgent logs?",
                "Confirm",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        _logService.Clear();
        _logService.LogInfo("Logs were cleared from Settings.");
        MessageBox.Show("Logs cleared.", "StudentAgent", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void saveButton_Click(object? sender, EventArgs e)
    {
        if (_settingsStore is null || _logService is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sharedSecretTextBox.Text))
        {
            MessageBox.Show("Shared secret is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!string.IsNullOrWhiteSpace(passwordTextBox.Text) &&
            !string.Equals(passwordTextBox.Text, confirmPasswordTextBox.Text, StringComparison.Ordinal))
        {
            MessageBox.Show("Passwords do not match.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settingsStore.UpdateCredentials(sharedSecretTextBox.Text, passwordTextBox.Text);
        _logService.LogInfo("StudentAgent settings were updated.");
        MessageBox.Show("Settings saved.", "StudentAgent", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Close();
    }

    private void cancelButton_Click(object? sender, EventArgs e)
    {
        Close();
    }
}
