#nullable enable

namespace TeacherClient;

public partial class AboutDialog : Form
{
    public AboutDialog()
    {
        InitializeComponent();
        versionValueLabel.Text = Application.ProductVersion;
    }
}
