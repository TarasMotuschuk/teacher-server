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

        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        Text = "About TeacherClient";
        Width = 860;
        Height = 500;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Padding = new Padding(22, 20, 22, 18);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 172F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));

        titleLabel.Dock = DockStyle.Fill;
        titleLabel.AutoSize = false;
        titleLabel.Font = new Font("Segoe UI", 16F, FontStyle.Bold, GraphicsUnit.Point);
        titleLabel.Text = "Teacher Classroom Client";
        titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        titleLabel.Margin = new Padding(0, 0, 0, 6);

        descriptionLabel.Dock = DockStyle.Fill;
        descriptionLabel.Text = "TeacherClient is the Windows desktop control panel for connecting to StudentAgent, viewing processes, and managing files in a transparent classroom environment.";
        descriptionLabel.TextAlign = ContentAlignment.TopLeft;
        descriptionLabel.Margin = new Padding(0, 4, 0, 8);

        versionLabel.Dock = DockStyle.Fill;
        versionLabel.Text = "Version:";
        versionLabel.TextAlign = ContentAlignment.MiddleLeft;
        versionLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);

        versionValueLabel.Dock = DockStyle.Fill;
        versionValueLabel.Text = "0.0.0";
        versionValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        versionValueLabel.AutoEllipsis = false;

        copyrightLabel.Dock = DockStyle.Fill;
        copyrightLabel.Text = "Copyright Taras Motuschuk";
        copyrightLabel.TextAlign = ContentAlignment.MiddleLeft;

        closeButton.Text = "Close";
        closeButton.Width = 120;
        closeButton.Height = 45;
        closeButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        closeButton.DialogResult = DialogResult.OK;

        layout.Controls.Add(titleLabel, 0, 0);
        layout.SetColumnSpan(titleLabel, 2);
        layout.Controls.Add(descriptionLabel, 0, 1);
        layout.SetColumnSpan(descriptionLabel, 2);
        layout.Controls.Add(versionLabel, 0, 2);
        layout.Controls.Add(versionValueLabel, 1, 2);
        layout.Controls.Add(copyrightLabel, 0, 3);
        layout.SetColumnSpan(copyrightLabel, 2);
        layout.Controls.Add(closeButton, 1, 4);

        Controls.Add(layout);

        AcceptButton = closeButton;
        CancelButton = closeButton;

        ResumeLayout(false);
    }
}
