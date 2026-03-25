#nullable enable

namespace StudentAgent.UI;

public partial class AboutForm : Form
{
    public AboutForm()
    {
        InitializeComponent();
        versionValueLabel.Text = Application.ProductVersion;
    }
}
