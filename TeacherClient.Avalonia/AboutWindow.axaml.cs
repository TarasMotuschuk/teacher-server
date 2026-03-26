using Avalonia.Controls;
using TeacherClient.CrossPlatform.Localization;

namespace TeacherClient.CrossPlatform;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        Title = CrossPlatformText.AboutWindowTitle;
        TitleTextBlock.Text = "TeacherClient.Avalonia";
        DescriptionTextBlock.Text = CrossPlatformText.AboutDescription;
        VersionTextBlock.Text = $"{CrossPlatformText.Version}: {GetType().Assembly.GetName().Version}";
        CopyrightTextBlock.Text = CrossPlatformText.Copyright;
        CloseButton.Content = CrossPlatformText.Close;
    }

    private void CloseButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
