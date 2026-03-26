using Avalonia.Controls;
using TeacherClient.CrossPlatform.Localization;

namespace TeacherClient.CrossPlatform.Dialogs;

public partial class TextInputDialog : Window
{
    public TextInputDialog()
    {
        InitializeComponent();
        OkButton.Content = CrossPlatformText.Ok;
        CancelButton.Content = CrossPlatformText.Cancel;
    }

    public static async Task<string?> ShowAsync(Window owner, string title, string prompt, string defaultValue = "")
    {
        var dialog = new TextInputDialog
        {
            Title = title
        };
        dialog.PromptTextBlock.Text = prompt;
        dialog.ValueTextBox.Text = defaultValue;
        return await dialog.ShowDialog<string?>(owner);
    }

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(ValueTextBox.Text?.Trim());
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}
