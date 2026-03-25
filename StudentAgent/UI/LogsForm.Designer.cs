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

        Text = "StudentAgent Logs";
        Width = 880;
        Height = 600;
        StartPosition = FormStartPosition.CenterScreen;

        logsTextBox.Multiline = true;
        logsTextBox.ScrollBars = ScrollBars.Both;
        logsTextBox.ReadOnly = true;
        logsTextBox.WordWrap = false;
        logsTextBox.Dock = DockStyle.Fill;
        logsTextBox.Font = new Font("Consolas", 10F);

        topPanel.Dock = DockStyle.Top;
        topPanel.Height = 48;

        refreshButton.Text = "Refresh";
        refreshButton.Left = 12;
        refreshButton.Top = 10;
        refreshButton.Width = 90;
        refreshButton.Height = 30;
        refreshButton.Click += refreshButton_Click;

        openFolderButton.Text = "Open log folder";
        openFolderButton.Left = 110;
        openFolderButton.Top = 10;
        openFolderButton.Width = 130;
        openFolderButton.Height = 30;
        openFolderButton.Click += openFolderButton_Click;

        topPanel.Controls.Add(refreshButton);
        topPanel.Controls.Add(openFolderButton);

        Controls.Add(logsTextBox);
        Controls.Add(topPanel);

        topPanel.ResumeLayout(false);
        ResumeLayout(false);
    }
}
