using Avalonia.Controls;
using TeacherClient.CrossPlatform.Localization;

namespace TeacherClient.CrossPlatform.Dialogs;

public partial class ConfirmationDialog : Window
{
    public ConfirmationDialog()
    {
        InitializeComponent();
        OkButton.Content = CrossPlatformText.Ok;
        CancelButton.Content = CrossPlatformText.Cancel;
    }

    public static async Task<bool> ShowAsync(Window owner, string title, string message)
    {
        var dialog = new ConfirmationDialog
        {
            Title = title,
        };
        dialog.MessageTextBlock.Text = message;
        return await dialog.ShowDialog<bool>(owner);
    }

    public static async Task ShowInfoAsync(Window owner, string title, string message)
    {
        var dialog = new ConfirmationDialog
        {
            Title = title,
        };
        dialog.MessageTextBlock.Text = message;
        dialog.CancelButton.IsVisible = false;
        dialog.OkButton.Content = CrossPlatformText.Ok;
        await dialog.ShowDialog<bool>(owner);
    }

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}
