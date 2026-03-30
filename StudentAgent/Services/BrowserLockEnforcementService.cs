using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using StudentAgent.UI.Localization;

namespace StudentAgent.Services;

public sealed class BrowserLockEnforcementService : BackgroundService
{
    private readonly AgentSettingsStore _settingsStore;
    private readonly AgentLogService _logService;
    private readonly ProcessService _processService;

    public BrowserLockEnforcementService(
        AgentSettingsStore settingsStore,
        AgentLogService logService,
        ProcessService processService)
    {
        _settingsStore = settingsStore;
        _logService = logService;
        _processService = processService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!_settingsStore.Current.BrowserLockEnabled)
            {
                continue;
            }

            if (Process.GetProcessesByName("StudentAgent.UIHost").Length > 0)
            {
                continue;
            }

            try
            {
                var killedCount = _processService.KillRunningBrowsers();
                if (killedCount > 0)
                {
                    StudentAgentText.SetLanguage(_settingsStore.Current.Language);
                    _logService.LogWarning(StudentAgentText.BrowserLockKilledBrowsersLog(killedCount));
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Background browser enforcement failed: {ex}");
            }
        }
    }
}
