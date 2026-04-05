using System.Diagnostics;
using StudentAgent.Services;

namespace StudentAgent.Service.Services;

/// <summary>
/// Stops session-hosted EXEs launched via <see cref="SessionProcessLauncher"/> (not child processes of the service).
/// Must run when the Windows service stops so uninstall can remove files and no orphans remain.
/// </summary>
internal static class SessionHostedProcessCleanup
{
    public static void StopAllByImageName(string processImageNameWithoutExtension, AgentLogService logService)
    {
        foreach (var process in Process.GetProcessesByName(processImageNameWithoutExtension))
        {
            try
            {
                int sessionId;
                try
                {
                    sessionId = process.SessionId;
                }
                catch
                {
                    sessionId = -1;
                }

                logService.LogInfo($"Stopping '{processImageNameWithoutExtension}' (PID {process.Id}, session {sessionId}).");
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                logService.LogWarning($"Failed to stop '{processImageNameWithoutExtension}': {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
