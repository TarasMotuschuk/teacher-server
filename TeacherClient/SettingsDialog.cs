#nullable enable

using Teacher.Common.Localization;
using TeacherClient.Localization;
using TeacherClient.Models;

namespace TeacherClient;

public partial class SettingsDialog : Form
{
    public SettingsDialog()
    {
        InitializeComponent();
        Icon = AppIconLoader.Load();
    }

    public SettingsDialog(ClientSettings settings)
        : this()
    {
        languageComboBox.Items.AddRange(["Українська", "English"]);
        languageComboBox.SelectedIndex = settings.Language == UiLanguage.Ukrainian ? 0 : 1;
        sharedSecretTextBox.Text = settings.SharedSecret;
        bulkCopyDestinationPathTextBox.Text = settings.BulkCopyDestinationPath;
        studentWorkRootPathTextBox.Text = settings.StudentWorkRootPath;
        studentWorkFolderNameTextBox.Text = settings.StudentWorkFolderName;
        desktopIconAutoRestoreIntervalNumeric.Value = settings.DesktopIconAutoRestoreMinutes;
        browserLockCheckIntervalNumeric.Value = settings.BrowserLockCheckIntervalSeconds;
        ApplyLocalization();
    }

    public ClientSettings ToSettings()
        => new(
            sharedSecretTextBox.Text.Trim(),
            languageComboBox.SelectedIndex == 0 ? UiLanguage.Ukrainian : UiLanguage.English,
            bulkCopyDestinationPathTextBox.Text.Trim(),
            studentWorkRootPathTextBox.Text.Trim(),
            studentWorkFolderNameTextBox.Text.Trim(),
            (int)desktopIconAutoRestoreIntervalNumeric.Value,
            (int)browserLockCheckIntervalNumeric.Value);

    private void ApplyLocalization()
    {
        Text = TeacherClientText.SettingsDialogTitle;
        sharedSecretLabel.Text = TeacherClientText.SharedSecret;
        bulkCopyDestinationPathLabel.Text = TeacherClientText.BulkCopyDestinationPath;
        studentWorkRootPathLabel.Text = TeacherClientText.StudentWorkRootPath;
        studentWorkFolderNameLabel.Text = TeacherClientText.StudentWorkFolderName;
        desktopIconAutoRestoreIntervalLabel.Text = TeacherClientText.DesktopIconAutoRestoreInterval;
        browserLockCheckIntervalLabel.Text = TeacherClientText.BrowserLockCheckInterval;
        languageLabel.Text = TeacherClientText.Language;
        hintLabel.Text = TeacherClientText.SettingsHint;
        saveButton.Text = TeacherClientText.Save;
        cancelButton.Text = TeacherClientText.Cancel;
    }
}
