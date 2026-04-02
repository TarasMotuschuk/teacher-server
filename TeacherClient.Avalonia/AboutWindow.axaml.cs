using Avalonia.Controls;
using TeacherClient.CrossPlatform.Localization;

namespace TeacherClient.CrossPlatform;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        Icon = AppIconLoader.Load();
        Title = CrossPlatformText.AboutWindowTitle;
        TitleTextBlock.Text = "ClassCommander";
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
