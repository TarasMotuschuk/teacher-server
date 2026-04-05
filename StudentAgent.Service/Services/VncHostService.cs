using System.Diagnostics;
using StudentAgent.Services;

namespace StudentAgent.Service.Services;

public sealed class VncHostService
{
    private readonly AgentLogService _logService;
    private readonly string _vncHostPath;

    public VncHostService(AgentLogService logService)
    {
        _logService = logService;
        _vncHostPath = Path.Combine(AppContext.BaseDirectory, "StudentAgent.VncHost.exe");
    }

    public bool IsRunningInSession(int sessionId)
    {
        return Process.GetProcessesByName("StudentAgent.VncHost").Any(process =>
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

    public void StartForSession(int sessionId)
    {
        if (!File.Exists(_vncHostPath))
        {
            _logService.LogWarning($"StudentAgent.VncHost was not found at '{_vncHostPath}'.");
            return;
        }

        if (IsRunningInSession(sessionId))
        {
            return;
        }

        SessionProcessLauncher.StartProcessInSession(_vncHostPath, string.Empty, sessionId);
        _logService.LogInfo($"Started StudentAgent.VncHost in session {sessionId}.");
    }

    public void StopInSession(int sessionId)
    {
        foreach (var process in Process.GetProcessesByName("StudentAgent.VncHost"))
        {
            try
            {
                if (process.SessionId != sessionId)
                {
                    continue;
                }

                _logService.LogInfo($"Stopping StudentAgent.VncHost in session {sessionId}.");
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Failed to stop StudentAgent.VncHost in session {sessionId}: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    public void StopAll()
    {
        foreach (var process in Process.GetProcessesByName("StudentAgent.VncHost"))
        {
            try
            {
                _logService.LogInfo($"Stopping StudentAgent.VncHost in session {process.SessionId}.");
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Failed to stop StudentAgent.VncHost in session {process.SessionId}: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
