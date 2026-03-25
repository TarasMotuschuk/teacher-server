#nullable enable

namespace StudentAgent.UI;

partial class AboutForm
{
    private System.ComponentModel.IContainer? components = null;
    private Label titleLabel = null!;
    private Label descriptionLabel = null!;
    private Label versionLabel = null!;
    private Label versionValueLabel = null!;
    private Label copyrightLabel = null!;
    private Button closeButton = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        titleLabel = new Label();
        descriptionLabel = new Label();
        versionLabel = new Label();
        versionValueLabel = new Label();
        copyrightLabel = new Label();
        closeButton = new Button();
        SuspendLayout();

        Text = "About StudentAgent";
        Width = 500;
        Height = 330;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        titleLabel.Left = 16;
        titleLabel.Top = 18;
        titleLabel.Width = 450;
        titleLabel.Height = 45;
        titleLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        titleLabel.Text = "StudentAgent";

        descriptionLabel.Left = 16;
        descriptionLabel.Top = 68;
        descriptionLabel.Width = 450;
        descriptionLabel.Height = 110;
        descriptionLabel.Text = "StudentAgent is the student-side classroom service. It exposes a visible, authorized management API and runs in the Windows system tray with protected settings and logs access.";

        versionLabel.Left = 16;
        versionLabel.Top = 184;
        versionLabel.Width = 80;
        versionLabel.Height = 45;
        versionLabel.Text = "Version:";

        versionValueLabel.Left = 100;
        versionValueLabel.Top = 184;
        versionValueLabel.Width = 220;
        versionValueLabel.Height = 45;
        versionValueLabel.Text = "0.0.0";

        copyrightLabel.Left = 16;
        copyrightLabel.Top = 224;
        copyrightLabel.Width = 340;
        copyrightLabel.Height = 45;
        copyrightLabel.Text = "Copyright Taras Motuschuk";

        closeButton.Text = "Close";
        closeButton.Left = 376;
        closeButton.Top = 246;
        closeButton.Width = 90;
        closeButton.Height = 45;
        closeButton.DialogResult = DialogResult.OK;

        Controls.Add(titleLabel);
        Controls.Add(descriptionLabel);
        Controls.Add(versionLabel);
        Controls.Add(versionValueLabel);
        Controls.Add(copyrightLabel);
        Controls.Add(closeButton);

        AcceptButton = closeButton;
        CancelButton = closeButton;

        ResumeLayout(false);
    }
}
