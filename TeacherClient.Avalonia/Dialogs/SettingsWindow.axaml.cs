using Avalonia.Controls;
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
        SharedSecretTextBox.Text = settings.SharedSecret;
    }

    public ClientSettings ToSettings()
        => new(SharedSecretTextBox.Text?.Trim() ?? string.Empty);

    private void SaveButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}
