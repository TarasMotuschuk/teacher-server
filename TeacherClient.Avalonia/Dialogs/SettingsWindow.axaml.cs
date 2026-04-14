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
        ThemeComboBox.SelectedIndex = (int)settings.Theme;
    }

    public ClientSettings ToSettings()
        => new(
            SharedSecretTextBox.Text?.Trim() ?? string.Empty,
            LanguageComboBox.SelectedIndex == 0 ? UiLanguage.Ukrainian : UiLanguage.English,
            BulkCopyDestinationPathTextBox.Text?.Trim() ?? string.Empty,
            StudentWorkRootPathTextBox.Text?.Trim() ?? string.Empty,
            StudentWorkFolderNameTextBox.Text?.Trim() ?? string.Empty,
            ParsePositiveInt(DesktopIconAutoRestoreIntervalTextBox.Text, ClientSettings.Default.DesktopIconAutoRestoreMinutes, 1),
            ParsePositiveInt(BrowserLockCheckIntervalTextBox.Text, ClientSettings.Default.BrowserLockCheckIntervalSeconds, 5),
            ThemeComboBox.SelectedIndex == 1 ? AppUiTheme.Light : AppUiTheme.Dark);

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
        ThemeLabel.Text = CrossPlatformText.SettingsUiTheme;
        var themeIndex = ThemeComboBox.SelectedIndex;
        ThemeComboBox.ItemsSource = new[] { CrossPlatformText.SettingsUiThemeDark, CrossPlatformText.SettingsUiThemeLight };
        ThemeComboBox.SelectedIndex = themeIndex >= 0 && themeIndex <= 1 ? themeIndex : 0;

        var tipSecret = CrossPlatformText.SettingsFieldTooltipSharedSecret;
        ToolTip.SetTip(SharedSecretLabel, tipSecret);
        ToolTip.SetTip(SharedSecretTextBox, tipSecret);

        var tipBulk = CrossPlatformText.SettingsFieldTooltipBulkCopyDestination;
        ToolTip.SetTip(BulkCopyDestinationPathLabel, tipBulk);
        ToolTip.SetTip(BulkCopyDestinationPathTextBox, tipBulk);

        var tipRoot = CrossPlatformText.SettingsFieldTooltipStudentWorkRootPath;
        ToolTip.SetTip(StudentWorkRootPathLabel, tipRoot);
        ToolTip.SetTip(StudentWorkRootPathTextBox, tipRoot);

        var tipFolderName = CrossPlatformText.SettingsFieldTooltipStudentWorkFolderName;
        ToolTip.SetTip(StudentWorkFolderNameLabel, tipFolderName);
        ToolTip.SetTip(StudentWorkFolderNameTextBox, tipFolderName);

        var tipInterval = CrossPlatformText.SettingsFieldTooltipTeacherSideInterval;
        ToolTip.SetTip(DesktopIconAutoRestoreIntervalLabel, tipInterval);
        ToolTip.SetTip(DesktopIconAutoRestoreIntervalTextBox, tipInterval);
        ToolTip.SetTip(BrowserLockCheckIntervalLabel, tipInterval);
        ToolTip.SetTip(BrowserLockCheckIntervalTextBox, tipInterval);

        SaveButton.Content = CrossPlatformText.Save;
        CancelButton.Content = CrossPlatformText.Cancel;
    }
}
