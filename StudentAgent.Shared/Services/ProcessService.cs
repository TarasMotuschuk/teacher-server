using System.Diagnostics;
using Teacher.Common.Contracts;

namespace StudentAgent.Services;

public sealed class ProcessService
{
    private static readonly HashSet<string> BrowserProcessNames =
    [
        "arc",
        "brave",
        "browser",
        "chrome",
        "firefox",
        "iexplore",
        "msedge",
        "opera",
        "vivaldi",
        "yandex"
    ];

    public IReadOnlyList<ProcessInfoDto> GetProcesses()
    {
        return Process.GetProcesses()
            .OrderBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(MapProcess)
            .ToList();
    }

    public bool KillProcess(int processId)
    {
        using var process = Process.GetProcessById(processId);
        process.Kill(entireProcessTree: true);
        return true;
    }

    public IReadOnlyList<ProcessInfoDto> GetRunningBrowsers()
    {
        return Process.GetProcesses()
            .Where(p => BrowserProcessNames.Contains(p.ProcessName))
            .OrderBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(MapProcess)
            .ToList();
    }

    public int KillRunningBrowsers()
    {
        var killed = 0;

        foreach (var process in Process.GetProcesses().Where(p => BrowserProcessNames.Contains(p.ProcessName)))
        {
            try
            {
                using (process)
                {
                    process.Kill(entireProcessTree: true);
                    killed++;
                }
            }
            catch
            {
            }
        }

        return killed;
    }

    public void ExecutePowerAction(PowerActionKind action)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Power actions are only supported on Windows student agents.");
        }

        var arguments = action switch
        {
            PowerActionKind.Shutdown => "/s /t 0 /f",
            PowerActionKind.Restart => "/r /t 0 /f",
            PowerActionKind.LogOff => "/l /f",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported power action.")
        };

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "shutdown.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        process.Start();
    }

    private static ProcessInfoDto MapProcess(Process process)
    {
        string? title = null;
        long workingSet = 0;
        DateTime startTimeUtc = DateTime.MinValue;
        var hasVisibleWindow = false;

        try
        {
            title = process.MainWindowTitle;
            hasVisibleWindow = !string.IsNullOrWhiteSpace(title);
        }
        catch
        {
        }

        try
        {
            workingSet = process.WorkingSet64;
        }
        catch
        {
        }

        try
        {
            startTimeUtc = process.StartTime.ToUniversalTime();
        }
        catch
        {
            startTimeUtc = DateTime.UtcNow;
        }

        return new ProcessInfoDto(
            process.Id,
            process.ProcessName,
            title,
            workingSet,
            startTimeUtc,
            hasVisibleWindow);
    }
}
