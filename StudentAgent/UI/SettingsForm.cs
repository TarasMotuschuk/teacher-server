using StudentAgent.Services;

namespace StudentAgent.UI;

public sealed class SettingsForm : Form
{
    private readonly AgentSettingsStore _settingsStore;
    private readonly AgentLogService _logService;
    private readonly TextBox _sharedSecretTextBox;
    private readonly TextBox _passwordTextBox;
    private readonly TextBox _confirmPasswordTextBox;

    public SettingsForm(AgentSettingsStore settingsStore, AgentLogService logService)
    {
        _settingsStore = settingsStore;
        _logService = logService;

        Text = "StudentAgent Settings";
        Width = 520;
        Height = 260;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var settings = _settingsStore.Current;

        var sharedSecretLabel = new Label
        {
            Left = 16,
            Top = 20,
            Width = 140,
            Text = "Shared secret"
        };

        _sharedSecretTextBox = new TextBox
        {
            Left = 160,
            Top = 16,
            Width = 320,
            Text = settings.SharedSecret
        };

        var passwordLabel = new Label
        {
            Left = 16,
            Top = 60,
            Width = 140,
            Text = "New password"
        };

        _passwordTextBox = new TextBox
        {
            Left = 160,
            Top = 56,
            Width = 320,
            UseSystemPasswordChar = true
        };

        var confirmPasswordLabel = new Label
        {
            Left = 16,
            Top = 100,
            Width = 140,
            Text = "Confirm password"
        };

        _confirmPasswordTextBox = new TextBox
        {
            Left = 160,
            Top = 96,
            Width = 320,
            UseSystemPasswordChar = true
        };

        var clearLogsButton = new Button
        {
            Text = "Clear logs",
            Left = 16,
            Top = 148,
            Width = 120,
            Height = 34
        };
        clearLogsButton.Click += ClearLogsButton_Click;

        var saveButton = new Button
        {
            Text = "Save",
            Left = 324,
            Top = 148,
            Width = 75,
            Height = 34
        };
        saveButton.Click += SaveButton_Click;

        var cancelButton = new Button
        {
            Text = "Cancel",
            Left = 405,
            Top = 148,
            Width = 75,
            Height = 34
        };
        cancelButton.Click += (_, _) => Close();

        Controls.Add(sharedSecretLabel);
        Controls.Add(_sharedSecretTextBox);
        Controls.Add(passwordLabel);
        Controls.Add(_passwordTextBox);
        Controls.Add(confirmPasswordLabel);
        Controls.Add(_confirmPasswordTextBox);
        Controls.Add(clearLogsButton);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);
    }

    private void ClearLogsButton_Click(object? sender, EventArgs e)
    {
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

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_sharedSecretTextBox.Text))
        {
            MessageBox.Show("Shared secret is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_passwordTextBox.Text) &&
            !string.Equals(_passwordTextBox.Text, _confirmPasswordTextBox.Text, StringComparison.Ordinal))
        {
            MessageBox.Show("Passwords do not match.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settingsStore.UpdateCredentials(_sharedSecretTextBox.Text, _passwordTextBox.Text);
        _logService.LogInfo("StudentAgent settings were updated.");
        MessageBox.Show("Settings saved.", "StudentAgent", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Close();
    }
}
