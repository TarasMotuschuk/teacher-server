using StudentAgent.Services;
using StudentAgent.UI;

namespace StudentAgent.UIHost;

public sealed class UIHostApplicationContext : AgentUiApplicationContextBase
{
    private readonly AgentLogService _logService;
    private readonly System.Windows.Forms.Timer _desktopIconRestoreTimer;

    public UIHostApplicationContext(AgentSettingsStore settingsStore, AgentLogService logService, ProcessService processService)
        : base(settingsStore, logService, processService)
    {
        _logService = logService;
        _desktopIconRestoreTimer = new System.Windows.Forms.Timer();
        _desktopIconRestoreTimer.Tick += (_, _) => RestoreDesktopIconsSilently();
        ConfigureDesktopIconRestoreTimer();
        _desktopIconRestoreTimer.Start();
        settingsStore.SettingsChanged += SettingsStore_OnSettingsChanged;
    }

    protected override void HandleExitRequested()
    {
        if (!PromptForPassword())
        {
            return;
        }

        ExitThread();
    }

    protected override void OnBeforeExitThreadCore()
    {
        SettingsStore.SettingsChanged -= SettingsStore_OnSettingsChanged;
        _desktopIconRestoreTimer.Stop();
        _desktopIconRestoreTimer.Dispose();
        _logService.LogInfo("StudentAgent.UIHost stopping.");
    }

    private void SettingsStore_OnSettingsChanged(object? sender, EventArgs e)
    {
        ConfigureDesktopIconRestoreTimer();
    }

    private void ConfigureDesktopIconRestoreTimer()
    {
        var minutes = Math.Max(1, SettingsStore.Current.DesktopIconAutoRestoreMinutes);
        _desktopIconRestoreTimer.Interval = checked((int)TimeSpan.FromMinutes(minutes).TotalMilliseconds);
    }

    private void RestoreDesktopIconsSilently()
    {
        try
        {
            var layoutPath = StudentAgentPathHelper.GetDesktopLayoutFilePath("default");
            if (!File.Exists(layoutPath))
            {
                return;
            }

            var args = new[]
            {
                "desktop-icons",
                "restore",
                "default",
                Path.Combine(StudentAgentPathHelper.GetDesktopLayoutResultsDirectory(), "timer-restore.json"),
            };

            _ = DesktopIcons.DesktopIconLayoutCommandRunner.TryExecute(args, _logService, out _, out var resultPath);

            if (!string.IsNullOrWhiteSpace(resultPath) && File.Exists(resultPath))
            {
                try
                {
                    File.Delete(resultPath);
                }
                catch
                {
                    // Ignore timer result cleanup failures.
                }
            }
        }
        catch
        {
            // Silent by design: this mirrors DesktopIconSaver's periodic restore behavior.
        }
    }
}
