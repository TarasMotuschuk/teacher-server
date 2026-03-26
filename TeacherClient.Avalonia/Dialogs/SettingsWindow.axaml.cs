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
    }

    public SettingsWindow(ClientSettings settings)
        : this()
    {
        LanguageComboBox.ItemsSource = new[] { "Українська", "English" };
        LanguageComboBox.SelectedIndex = settings.Language == UiLanguage.Ukrainian ? 0 : 1;
        SharedSecretTextBox.Text = settings.SharedSecret;
        ApplyLocalization();
    }

    public ClientSettings ToSettings()
        => new(
            SharedSecretTextBox.Text?.Trim() ?? string.Empty,
            LanguageComboBox.SelectedIndex == 0 ? UiLanguage.Ukrainian : UiLanguage.English);

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
        LanguageLabel.Text = CrossPlatformText.Language;
        HintTextBlock.Text = CrossPlatformText.SettingsHint;
        SaveButton.Content = CrossPlatformText.Save;
        CancelButton.Content = CrossPlatformText.Cancel;
    }
}
