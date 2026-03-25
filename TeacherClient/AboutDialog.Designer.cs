#nullable enable

namespace TeacherClient;

partial class AboutDialog
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

        Text = "About TeacherClient";
        Width = 480;
        Height = 320;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        titleLabel.Left = 16;
        titleLabel.Top = 18;
        titleLabel.Width = 430;
        titleLabel.Height = 45;
        titleLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        titleLabel.Text = "Teacher Classroom Client";

        descriptionLabel.Left = 16;
        descriptionLabel.Top = 68;
        descriptionLabel.Width = 430;
        descriptionLabel.Height = 90;
        descriptionLabel.Text = "TeacherClient is the Windows desktop control panel for connecting to StudentAgent, viewing processes, and managing files in a transparent classroom environment.";

        versionLabel.Left = 16;
        versionLabel.Top = 166;
        versionLabel.Width = 80;
        versionLabel.Height = 45;
        versionLabel.Text = "Version:";

        versionValueLabel.Left = 100;
        versionValueLabel.Top = 166;
        versionValueLabel.Width = 220;
        versionValueLabel.Height = 45;
        versionValueLabel.Text = "0.0.0";

        copyrightLabel.Left = 16;
        copyrightLabel.Top = 206;
        copyrightLabel.Width = 320;
        copyrightLabel.Height = 45;
        copyrightLabel.Text = "Copyright Taras Motuschuk";

        closeButton.Text = "Close";
        closeButton.Left = 356;
        closeButton.Top = 232;
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
