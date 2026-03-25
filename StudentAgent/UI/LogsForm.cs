using StudentAgent.Services;

namespace StudentAgent.UI;

public sealed class LogsForm : Form
{
    private readonly AgentLogService _logService;
    private readonly TextBox _logsTextBox;

    public LogsForm(AgentLogService logService)
    {
        _logService = logService;

        Text = "StudentAgent Logs";
        Width = 880;
        Height = 600;
        StartPosition = FormStartPosition.CenterScreen;

        _logsTextBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            WordWrap = false,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10)
        };

        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48
        };

        var refreshButton = new Button
        {
            Text = "Refresh",
            Left = 12,
            Top = 10,
            Width = 90,
            Height = 30
        };
        refreshButton.Click += (_, _) => LoadLogs();

        var openFolderButton = new Button
        {
            Text = "Open log folder",
            Left = 110,
            Top = 10,
            Width = 130,
            Height = 30
        };
        openFolderButton.Click += (_, _) =>
        {
            var directory = Path.GetDirectoryName(_logService.LogFilePath) ?? AppContext.BaseDirectory;
            System.Diagnostics.Process.Start("explorer.exe", directory);
        };

        topPanel.Controls.Add(refreshButton);
        topPanel.Controls.Add(openFolderButton);

        Controls.Add(_logsTextBox);
        Controls.Add(topPanel);

        LoadLogs();
    }

    private void LoadLogs()
    {
        _logsTextBox.Text = _logService.ReadAll();
    }
}
