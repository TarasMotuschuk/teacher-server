#nullable enable

using TeacherClient.Localization;

namespace TeacherClient;

public partial class AboutDialog : Form
{
    public AboutDialog()
    {
        InitializeComponent();
        Icon = AppIconLoader.Load();
        BackgroundImage = BrandingResourceLoader.LoadBitmap(@"Backgrounds/teacher-about.png");
        BackgroundImageLayout = ImageLayout.Stretch;
        Text = TeacherClientText.AboutTitle;
        titleLabel.Text = TeacherClientText.MainTitle;
        descriptionLabel.Text = TeacherClientText.AboutDescription;
        versionLabel.Text = TeacherClientText.Version;
        closeButton.Text = TeacherClientText.Close;
        versionValueLabel.Text = Application.ProductVersion;
    }
}
