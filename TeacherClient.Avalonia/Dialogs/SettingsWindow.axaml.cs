using System.Globalization;
using Avalonia.Controls;
using Teacher.Common.Localization;
using TeacherClient.CrossPlatform.Localization;
using TeacherClient.CrossPlatform.Models;

namespace TeacherClient.CrossPlatform.Dialogs;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Icon = AppIconLoader.Load();
    }

    public SettingsWindow(ClientSettings settings)
        : this()
    {
        LanguageComboBox.ItemsSource = new[] { "Українська", "English" };
        LanguageComboBox.SelectedIndex = settings.Language == UiLanguage.Ukrainian ? 0 : 1;
        SharedSecretTextBox.Text = settings.SharedSecret;
        BulkCopyDestinationPathTextBox.Text = settings.BulkCopyDestinationPath;
        StudentWorkRootPathTextBox.Text = settings.StudentWorkRootPath;
        StudentWorkFolderNameTextBox.Text = settings.StudentWorkFolderName;
        DesktopIconAutoRestoreIntervalTextBox.Text = settings.DesktopIconAutoRestoreMinutes.ToString(CultureInfo.InvariantCulture);
        BrowserLockCheckIntervalTextBox.Text = settings.BrowserLockCheckIntervalSeconds.ToString(CultureInfo.InvariantCulture);
        ApplyLocalization();
    }

    public ClientSettings ToSettings()
        => new(
            SharedSecretTextBox.Text?.Trim() ?? string.Empty,
            LanguageComboBox.SelectedIndex == 0 ? UiLanguage.Ukrainian : UiLanguage.English,
            BulkCopyDestinationPathTextBox.Text?.Trim() ?? string.Empty,
            StudentWorkRootPathTextBox.Text?.Trim() ?? string.Empty,
            StudentWorkFolderNameTextBox.Text?.Trim() ?? string.Empty,
            ParsePositiveInt(DesktopIconAutoRestoreIntervalTextBox.Text, ClientSettings.Default.DesktopIconAutoRestoreMinutes, 1),
            ParsePositiveInt(BrowserLockCheckIntervalTextBox.Text, ClientSettings.Default.BrowserLockCheckIntervalSeconds, 5));

    private static int ParsePositiveInt(string? value, int fallback, int minValue)
    {
        if (!int.TryParse(value, out var parsed))
        {
            return fallback;
        }

        return Math.Max(minValue, parsed);
    }

    private void SaveButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }

    private void ApplyLocalization()
    {
        Title = CrossPlatformText.SettingsWindowTitle;
        SharedSecretLabel.Text = CrossPlatformText.SharedSecret;
        BulkCopyDestinationPathLabel.Text = CrossPlatformText.BulkCopyDestinationPath;
        StudentWorkRootPathLabel.Text = CrossPlatformText.StudentWorkRootPath;
        StudentWorkFolderNameLabel.Text = CrossPlatformText.StudentWorkFolderName;
        DesktopIconAutoRestoreIntervalLabel.Text = CrossPlatformText.DesktopIconAutoRestoreInterval;
        BrowserLockCheckIntervalLabel.Text = CrossPlatformText.BrowserLockCheckInterval;
        LanguageLabel.Text = CrossPlatformText.Language;
        HintTextBlock.Text = CrossPlatformText.SettingsHint;
        SaveButton.Content = CrossPlatformText.Save;
        CancelButton.Content = CrossPlatformText.Cancel;
    }
}
