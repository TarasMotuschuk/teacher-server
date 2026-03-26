#nullable enable

using StudentAgent.UI.Localization;

namespace StudentAgent.UI;

public partial class AboutForm : Form
{
    public AboutForm()
    {
        InitializeComponent();
        Text = StudentAgentText.AboutTitle;
        titleLabel.Text = StudentAgentText.AgentName;
        descriptionLabel.Text = StudentAgentText.AboutDescription;
        versionLabel.Text = StudentAgentText.Version;
        closeButton.Text = StudentAgentText.Close;
        versionValueLabel.Text = Application.ProductVersion;
    }
}
