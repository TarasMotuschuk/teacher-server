using Avalonia.Controls;

namespace TeacherClient.CrossPlatform.Dialogs;

public partial class ConfirmationDialog : Window
{
    public ConfirmationDialog()
    {
        InitializeComponent();
    }

    public static async Task<bool> ShowAsync(Window owner, string title, string message)
    {
        var dialog = new ConfirmationDialog
        {
            Title = title
        };
        dialog.MessageTextBlock.Text = message;
        return await dialog.ShowDialog<bool>(owner);
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
