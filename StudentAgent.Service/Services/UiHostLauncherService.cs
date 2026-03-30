using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using StudentAgent.Services;

namespace StudentAgent.Service.Services;

public sealed class UiHostLauncherService : BackgroundService
{
    private readonly AgentLogService _logService;
    private readonly string _uiHostPath;
    private DateTime _lastMissingBinaryLogUtc;

    public UiHostLauncherService(AgentLogService logService)
    {
        _logService = logService;
        _uiHostPath = Path.Combine(AppContext.BaseDirectory, "StudentAgent.UIHost.exe");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

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

            if (!File.Exists(_uiHostPath))
            {
                if (DateTime.UtcNow - _lastMissingBinaryLogUtc > TimeSpan.FromMinutes(5))
                {
                    _lastMissingBinaryLogUtc = DateTime.UtcNow;
                    _logService.LogWarning($"StudentAgent.UIHost was not found at '{_uiHostPath}'.");
                }

                continue;
            }

            var sessionId = SessionProcessLauncher.GetActiveSessionId();
            if (sessionId < 0)
            {
                continue;
            }

            if (IsUiHostRunning(sessionId))
            {
                continue;
            }

            try
            {
                SessionProcessLauncher.StartProcessInSession(_uiHostPath, string.Empty, sessionId);
                _logService.LogInfo($"Started StudentAgent.UIHost in session {sessionId}.");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to start StudentAgent.UIHost in session {sessionId}: {ex}");
            }
        }
    }

    private static bool IsUiHostRunning(int sessionId)
    {
        return Process.GetProcessesByName("StudentAgent.UIHost").Any(process =>
        {
            try
            {
                return process.SessionId == sessionId;
            }
            catch
            {
                return false;
            }
            finally
            {
                process.Dispose();
            }
        });
    }
}
