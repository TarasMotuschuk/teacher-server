using StudentAgent.Services;
using StudentAgent.UI.Localization;
using Teacher.Common.Localization;

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

        languageComboBox.Items.AddRange(["Українська", "English"]);
        var settings = _settingsStore.Current;
        sharedSecretTextBox.Text = settings.SharedSecret;
        languageComboBox.SelectedIndex = settings.Language == UiLanguage.Ukrainian ? 0 : 1;
        ApplyLocalization();
    }

    private void ClearLogsButton_Click(object? sender, EventArgs e)
    {
        if (_logService is null)
        {
            return;
        }

        if (MessageBox.Show(
                StudentAgentText.ClearAllLogsPrompt,
                StudentAgentText.Confirm,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        _logService.Clear();
        _logService.LogInfo("Logs were cleared from Settings.");
        MessageBox.Show(StudentAgentText.LogsCleared, StudentAgentText.AgentName, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        if (_settingsStore is null || _logService is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sharedSecretTextBox.Text))
        {
            MessageBox.Show(StudentAgentText.SharedSecretRequired, StudentAgentText.Validation, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!string.IsNullOrWhiteSpace(passwordTextBox.Text) &&
            !string.Equals(passwordTextBox.Text, confirmPasswordTextBox.Text, StringComparison.Ordinal))
        {
            MessageBox.Show(StudentAgentText.PasswordsMismatch, StudentAgentText.Validation, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var language = languageComboBox.SelectedIndex == 0 ? UiLanguage.Ukrainian : UiLanguage.English;
        StudentAgentText.SetLanguage(language);
        _settingsStore.UpdateSettings(sharedSecretTextBox.Text, passwordTextBox.Text, language);
        _logService.LogInfo("StudentAgent settings were updated.");
        MessageBox.Show(StudentAgentText.SettingsSaved, StudentAgentText.AgentName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        Close();
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        Close();
    }

    private void ApplyLocalization()
    {
        Text = StudentAgentText.SettingsTitle;
        sharedSecretLabel.Text = StudentAgentText.SharedSecret;
        passwordLabel.Text = StudentAgentText.NewPassword;
        confirmPasswordLabel.Text = StudentAgentText.ConfirmPassword;
        languageLabel.Text = StudentAgentText.Language;
        clearLogsButton.Text = StudentAgentText.ClearLogs;
        saveButton.Text = StudentAgentText.Save;
        cancelButton.Text = StudentAgentText.Cancel;
    }
}
