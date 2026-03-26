using System.Security.Principal;
using StudentAgent.Services;
using StudentAgent.UI.Localization;

namespace StudentAgent.UI;

public sealed class AgentApplicationContext : ApplicationContext
{
    private readonly WebApplication _app;
    private readonly AgentSettingsStore _settingsStore;
    private readonly AgentLogService _logService;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _aboutMenuItem;
    private readonly ToolStripMenuItem _settingsMenuItem;
    private readonly ToolStripMenuItem _logsMenuItem;
    private readonly ToolStripMenuItem _exitMenuItem;

    public AgentApplicationContext(WebApplication app, AgentSettingsStore settingsStore, AgentLogService logService)
    {
        _app = app;
        _settingsStore = settingsStore;
        _logService = logService;
        StudentAgentText.SetLanguage(_settingsStore.Current.Language);

        var menu = new ContextMenuStrip();
        _aboutMenuItem = new ToolStripMenuItem();
        _settingsMenuItem = new ToolStripMenuItem();
        _logsMenuItem = new ToolStripMenuItem();
        _exitMenuItem = new ToolStripMenuItem();
        _aboutMenuItem.Click += (_, _) => OpenAbout();
        _settingsMenuItem.Click += (_, _) => OpenSettings();
        _logsMenuItem.Click += (_, _) => OpenLogs();
        _exitMenuItem.Click += (_, _) => ExitAgent();
        menu.Items.Add(_aboutMenuItem);
        menu.Items.Add(_settingsMenuItem);
        menu.Items.Add(_logsMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitMenuItem);

        _notifyIcon = new NotifyIcon
        {
            Text = StudentAgentText.AgentName,
            Icon = SystemIcons.Shield,
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => OpenSettings();
        ApplyLocalization();
        //_notifyIcon.ShowBalloonTip(2000, StudentAgentText.AgentName, StudentAgentText.TrayBalloon, ToolTipIcon.Info);
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
        ApplyLocalization();
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

    private void OpenAbout()
    {
        using var form = new AboutForm();
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
            _logService.LogWarning(StudentAgentText.ExitDeniedBecauseNotAdminLog);
            MessageBox.Show(
                StudentAgentText.OnlyAdminCanClose,
                StudentAgentText.ExitDenied,
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
            _logService.LogWarning(StudentAgentText.ProtectedMenuAccessDeniedLog);
            MessageBox.Show(
                StudentAgentText.InvalidPassword,
                StudentAgentText.AccessDenied,
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

    private void ApplyLocalization()
    {
        StudentAgentText.SetLanguage(_settingsStore.Current.Language);
        _aboutMenuItem.Text = StudentAgentText.About;
        _settingsMenuItem.Text = StudentAgentText.Settings;
        _logsMenuItem.Text = StudentAgentText.Logs;
        _exitMenuItem.Text = StudentAgentText.Exit;
        _notifyIcon.Text = StudentAgentText.AgentName;
    }
}
