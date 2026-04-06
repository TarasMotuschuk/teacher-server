using Microsoft.Extensions.Hosting;
using StudentAgent;
using StudentAgent.Services;

namespace StudentAgent.Service.Services;

public sealed class VncHostLauncherService : BackgroundService
{
    private readonly AgentLogService _logService;
    private readonly AgentSettingsStore _settingsStore;
    private readonly VncHostService _vncHostService;
    private string _lastAppliedSignature = string.Empty;

    public VncHostLauncherService(
        AgentLogService logService,
        AgentSettingsStore settingsStore,
        VncHostService vncHostService)
    {
        _logService = logService;
        _settingsStore = settingsStore;
        _vncHostService = vncHostService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Match Veyon-style service polling: re-check console session and VNC host soon after logon/logoff.
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

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

            if (!OperatingSystem.IsWindows())
            {
                continue;
            }

            var sessionId = SessionProcessLauncher.GetActiveSessionId();
            if (sessionId < 0)
            {
                continue;
            }

            var enabled = _settingsStore.Current.VncEnabled;
            var signature = BuildSignature(_settingsStore.Current);
            if (!enabled)
            {
                _vncHostService.StopAll();
                _lastAppliedSignature = string.Empty;
                continue;
            }

            try
            {
                if (!_vncHostService.IsRunningInSession(sessionId) || !string.Equals(_lastAppliedSignature, signature, StringComparison.Ordinal))
                {
                    _vncHostService.StopAll();
                    _vncHostService.StartForSession(sessionId);
                    _lastAppliedSignature = signature;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to manage StudentAgent.VncHost in session {sessionId}: {ex}");
            }
        }
    }

    private static string BuildSignature(AgentRuntimeSettings settings)
        => string.Join('|',
            settings.VncEnabled,
            settings.VncPort,
            settings.VncViewOnly,
            settings.VncPassword);
}
