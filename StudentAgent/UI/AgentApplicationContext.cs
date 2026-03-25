using System.Security.Principal;
using StudentAgent.Services;

namespace StudentAgent.UI;

public sealed class AgentApplicationContext : ApplicationContext
{
    private readonly WebApplication _app;
    private readonly AgentSettingsStore _settingsStore;
    private readonly AgentLogService _logService;
    private readonly NotifyIcon _notifyIcon;

    public AgentApplicationContext(WebApplication app, AgentSettingsStore settingsStore, AgentLogService logService)
    {
        _app = app;
        _settingsStore = settingsStore;
        _logService = logService;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings", null, (_, _) => OpenSettings());
        menu.Items.Add("Logs", null, (_, _) => OpenLogs());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitAgent());

        _notifyIcon = new NotifyIcon
        {
            Text = "StudentAgent",
            Icon = SystemIcons.Shield,
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => OpenSettings();
        _notifyIcon.ShowBalloonTip(2000, "StudentAgent", "StudentAgent is running in the system tray.", ToolTipIcon.Info);
    }

    protected override void ExitThreadCore()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _logService.LogInfo("StudentAgent stopping.");
        _app.StopAsync().GetAwaiter().GetResult();
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.ExitThreadCore();
    }

    private void OpenSettings()
    {
        if (!PromptForPassword())
        {
            return;
        }

        using var form = new SettingsForm(_settingsStore, _logService);
        form.ShowDialog();
    }

    private void OpenLogs()
    {
        if (!PromptForPassword())
        {
            return;
        }

        using var form = new LogsForm(_logService);
        form.ShowDialog();
    }

    private void ExitAgent()
    {
        if (!PromptForPassword())
        {
            return;
        }

        if (!IsAdministrator())
        {
            _logService.LogWarning("Exit denied because current user is not an administrator.");
            MessageBox.Show(
                "Only a Windows administrator can close StudentAgent.",
                "Exit denied",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        ExitThread();
    }

    private bool PromptForPassword()
    {
        using var dialog = new PasswordPromptForm();
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return false;
        }

        var isValid = _settingsStore.VerifyPassword(dialog.Password);
        if (!isValid)
        {
            _logService.LogWarning("Protected menu access denied because of an invalid password.");
            MessageBox.Show(
                "Invalid password.",
                "Access denied",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        return isValid;
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
