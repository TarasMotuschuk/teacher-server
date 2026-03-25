using Avalonia.Controls;

namespace TeacherClient.CrossPlatform;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionTextBlock.Text = $"Version: {GetType().Assembly.GetName().Version}";
    }

    private void CloseButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
