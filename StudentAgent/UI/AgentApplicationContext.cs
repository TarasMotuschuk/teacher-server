using System.Security.Principal;
using StudentAgent.Services;
using StudentAgent.UI.Localization;

namespace StudentAgent.UI;

public sealed class AgentApplicationContext : ApplicationContext
{
    private readonly WebApplication _app;
    private readonly AgentSettingsStore _settingsStore;
    private readonly AgentLogService _logService;
    private readonly ProcessService _processService;
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _browserLockTimer;
    private readonly System.Windows.Forms.Timer _inputLockRefreshTimer;
    private readonly ToolStripMenuItem _aboutMenuItem;
    private readonly ToolStripMenuItem _settingsMenuItem;
    private readonly ToolStripMenuItem _logsMenuItem;
    private readonly ToolStripMenuItem _exitMenuItem;
    private readonly List<InputLockForm> _inputLockForms = [];
    private bool _browserCheckInProgress;

    public AgentApplicationContext(WebApplication app, AgentSettingsStore settingsStore, AgentLogService logService, ProcessService processService)
    {
        _app = app;
        _settingsStore = settingsStore;
        _logService = logService;
        _processService = processService;
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

        _browserLockTimer = new System.Windows.Forms.Timer
        {
            Interval = (int)TimeSpan.FromMinutes(1).TotalMilliseconds
        };
        _browserLockTimer.Tick += async (_, _) => await EvaluateBrowserLockAsync();
        _browserLockTimer.Start();

        _inputLockRefreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000
        };
        _inputLockRefreshTimer.Tick += (_, _) => EnsureInputLockForms();
        _inputLockRefreshTimer.Start();
        EnsureInputLockForms();
    }

    protected override void ExitThreadCore()
    {
        _browserLockTimer.Stop();
        _browserLockTimer.Dispose();
        _inputLockRefreshTimer.Stop();
        _inputLockRefreshTimer.Dispose();
        CloseInputLockForms();
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
        foreach (var form in _inputLockForms.ToArray())
        {
            form.ForceClose();
        }

        _inputLockForms.Clear();
        EnsureInputLockForms();
    }

    private async Task EvaluateBrowserLockAsync()
    {
        if (_browserCheckInProgress || !_settingsStore.Current.BrowserLockEnabled)
        {
            return;
        }

        var browsers = _processService.GetRunningBrowsers();
        if (browsers.Count == 0)
        {
            return;
        }

        _browserCheckInProgress = true;

        try
        {
            using var warningForm = new BrowserLockWarningForm(StudentAgentText.BrowserUsageForbiddenMessage, 10);
            warningForm.Show();
            await Task.Delay(TimeSpan.FromSeconds(10));

            if (!_settingsStore.Current.BrowserLockEnabled)
            {
                warningForm.Close();
                return;
            }

            var killedCount = _processService.KillRunningBrowsers();
            _logService.LogWarning(StudentAgentText.BrowserLockKilledBrowsersLog(killedCount));
            warningForm.Close();
        }
        finally
        {
            _browserCheckInProgress = false;
        }
    }

    private void EnsureInputLockForms()
    {
        if (_settingsStore.Current.InputLockEnabled)
        {
            if (_inputLockForms.Count == 0)
            {
                foreach (var screen in Screen.AllScreens)
                {
                    var form = new InputLockForm(screen);
                    _inputLockForms.Add(form);
                    form.Show();
                }
            }

            foreach (var form in _inputLockForms)
            {
                form.TopMost = true;
                form.Show();
                form.Activate();
                form.BringToFront();
            }

            return;
        }

        CloseInputLockForms();
    }

    private void CloseInputLockForms()
    {
        foreach (var form in _inputLockForms.ToArray())
        {
            form.ForceClose();
        }

        _inputLockForms.Clear();
    }
}
