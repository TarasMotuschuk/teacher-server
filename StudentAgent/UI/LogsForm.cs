using StudentAgent.Services;

namespace StudentAgent.UI;

public partial class LogsForm : Form
{
    private AgentLogService? _logService;

    public LogsForm()
    {
        InitializeComponent();
    }

    public LogsForm(AgentLogService logService)
        : this()
    {
        _logService = logService;
        LoadLogs();
    }

    private void refreshButton_Click(object? sender, EventArgs e)
    {
        LoadLogs();
    }

    private void openFolderButton_Click(object? sender, EventArgs e)
    {
        if (_logService is null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_logService.LogFilePath) ?? AppContext.BaseDirectory;
        System.Diagnostics.Process.Start("explorer.exe", directory);
    }

    private void LoadLogs()
    {
        if (_logService is null)
        {
            logsTextBox.Text = string.Empty;
            return;
        }

        logsTextBox.Text = _logService.ReadAll();
    }
}
