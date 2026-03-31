using StudentAgent.Services;

namespace StudentAgent.Service.Services;

public sealed class RemoteShellOpenService
{
    private readonly AgentLogService _logService;

    public RemoteShellOpenService(AgentLogService logService)
    {
        _logService = logService;
    }

    public void OpenEntry(string fullPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Remote shell open is only supported on Windows student agents.");
        }

        var sessionId = SessionProcessLauncher.GetActiveSessionId();
        if (sessionId < 0)
        {
            throw new InvalidOperationException("No active interactive user session was found.");
        }

        var normalizedPath = Path.GetFullPath(fullPath);
        SessionProcessLauncher.StartShellOpenInSession(normalizedPath, sessionId);
        _logService.LogInfo($"Opened remote entry '{normalizedPath}' in session {sessionId}.");
    }
}
