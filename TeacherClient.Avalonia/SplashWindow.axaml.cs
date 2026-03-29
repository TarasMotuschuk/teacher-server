using TeacherClient.CrossPlatform.Localization;

namespace TeacherClient.CrossPlatform;

public partial class SplashWindow : Avalonia.Controls.Window
{
    public SplashWindow()
    {
        InitializeComponent();
        TitleTextBlock.Text = CrossPlatformText.SplashTitle;
        SubtitleTextBlock.Text = CrossPlatformText.SplashSubtitle;
    }
}
