#nullable enable

using TeacherClient.Localization;

namespace TeacherClient;

partial class ProcessDetailsDialog
{
    private System.ComponentModel.IContainer? components = null;
    private TextBox detailsTextBox = null!;
    private Button killButton = null!;
    private Button restartButton = null!;
    private Button closeButton = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        detailsTextBox = new TextBox();
        killButton = new Button();
        restartButton = new Button();
        closeButton = new Button();
        SuspendLayout();

        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(860, 640);
        MinimumSize = new Size(760, 560);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        StartPosition = FormStartPosition.CenterParent;
        Text = TeacherClientText.ProcessDetailsTitle;

        detailsTextBox.Dock = DockStyle.Fill;
        detailsTextBox.Multiline = true;
        detailsTextBox.ReadOnly = true;
        detailsTextBox.ScrollBars = ScrollBars.Vertical;
        detailsTextBox.BackColor = Color.White;
        detailsTextBox.Margin = new Padding(0);

        killButton.Text = TeacherClientText.TerminateSelected;
        killButton.MinimumSize = new Size(140, 46);
        killButton.AutoSize = true;
        killButton.Click += killButton_Click;

        restartButton.Text = TeacherClientText.RestartSelected;
        restartButton.MinimumSize = new Size(160, 46);
        restartButton.AutoSize = true;
        restartButton.Click += restartButton_Click;

        closeButton.Text = TeacherClientText.Close;
        closeButton.MinimumSize = new Size(120, 46);
        closeButton.AutoSize = true;
        closeButton.DialogResult = DialogResult.Cancel;

        var buttonLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        buttonLayout.Controls.Add(closeButton);
        buttonLayout.Controls.Add(restartButton);
        buttonLayout.Controls.Add(killButton);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(detailsTextBox, 0, 0);
        root.Controls.Add(buttonLayout, 0, 1);

        Controls.Add(root);
        AcceptButton = restartButton;
        CancelButton = closeButton;
        ResumeLayout(false);
    }
}
