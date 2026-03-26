#nullable enable

namespace StudentAgent.UI;

partial class LogsForm
{
    private System.ComponentModel.IContainer? components = null;
    private TextBox logsTextBox = null!;
    private Panel topPanel = null!;
    private Button refreshButton = null!;
    private Button openFolderButton = null!;

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
        logsTextBox = new TextBox();
        topPanel = new Panel();
        refreshButton = new Button();
        openFolderButton = new Button();
        topPanel.SuspendLayout();
        SuspendLayout();

        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        Text = "StudentAgent Logs";
        Width = 880;
        Height = 600;
        StartPosition = FormStartPosition.CenterScreen;
        Padding = new Padding(12);

        logsTextBox.Multiline = true;
        logsTextBox.ScrollBars = ScrollBars.Both;
        logsTextBox.ReadOnly = true;
        logsTextBox.WordWrap = false;
        logsTextBox.Dock = DockStyle.Fill;
        logsTextBox.Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point);

        topPanel.Dock = DockStyle.Top;
        topPanel.Height = 64;
        topPanel.Padding = new Padding(0, 0, 0, 8);

        refreshButton.Text = "Refresh";
        refreshButton.Width = 90;
        refreshButton.Height = 45;
        refreshButton.Left = 0;
        refreshButton.Top = 6;
        refreshButton.Click += refreshButton_Click;

        openFolderButton.Text = "Open log folder";
        openFolderButton.Width = 130;
        openFolderButton.Height = 45;
        openFolderButton.Left = 102;
        openFolderButton.Top = 6;
        openFolderButton.Click += openFolderButton_Click;

        topPanel.Controls.Add(refreshButton);
        topPanel.Controls.Add(openFolderButton);

        Controls.Add(logsTextBox);
        Controls.Add(topPanel);

        topPanel.ResumeLayout(false);
        ResumeLayout(false);
    }
}
