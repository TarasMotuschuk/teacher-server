using System.Diagnostics;
using System.Management;
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

    public ProcessDetailsDto GetProcessDetails(int processId)
    {
        using var process = Process.GetProcessById(processId);
        return MapProcessDetails(process);
    }

    public ProcessDetailsDto RestartProcess(int processId)
    {
        using var process = Process.GetProcessById(processId);
        var details = MapProcessDetails(process);
        var executablePath = details.ExecutablePath;

        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            throw new InvalidOperationException("Executable path is unavailable for this process.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = true
        };

        if (!string.IsNullOrWhiteSpace(details.CommandLine))
        {
            var arguments = ExtractArguments(details.CommandLine, executablePath);
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                startInfo.Arguments = arguments;
            }
        }

        Process.Start(startInfo);
        process.Kill(entireProcessTree: true);
        return details;
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

    private static ProcessDetailsDto MapProcessDetails(Process process)
    {
        string? title = null;
        string? executablePath = null;
        string? commandLine = null;
        string? priorityClass = null;
        string? fileVersion = null;
        string? productName = null;
        string? errorMessage = null;
        long workingSet = 0;
        DateTime startTimeUtc = DateTime.MinValue;
        var hasVisibleWindow = false;
        var responding = false;
        var sessionId = -1;
        var threadCount = 0;
        var handleCount = 0;
        var totalProcessorTime = TimeSpan.Zero;

        try
        {
            title = process.MainWindowTitle;
            hasVisibleWindow = !string.IsNullOrWhiteSpace(title);
        }
        catch (Exception ex)
        {
            errorMessage ??= ex.Message;
        }

        try
        {
            responding = process.Responding;
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

        try
        {
            sessionId = process.SessionId;
        }
        catch
        {
        }

        try
        {
            threadCount = process.Threads.Count;
        }
        catch
        {
        }

        try
        {
            handleCount = process.HandleCount;
        }
        catch
        {
        }

        try
        {
            totalProcessorTime = process.TotalProcessorTime;
        }
        catch
        {
        }

        try
        {
            priorityClass = process.PriorityClass.ToString();
        }
        catch
        {
        }

        try
        {
            executablePath = process.MainModule?.FileName;
        }
        catch
        {
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath))
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(executablePath);
                fileVersion = versionInfo.FileVersion;
                productName = versionInfo.ProductName;
            }
        }
        catch
        {
        }

        try
        {
            commandLine = TryGetCommandLine(process.Id);
        }
        catch
        {
        }

        return new ProcessDetailsDto(
            process.Id,
            process.ProcessName,
            title,
            executablePath,
            commandLine,
            workingSet,
            startTimeUtc,
            hasVisibleWindow,
            responding,
            sessionId,
            threadCount,
            handleCount,
            priorityClass,
            totalProcessorTime,
            fileVersion,
            productName,
            errorMessage);
    }

    private static string? TryGetCommandLine(int processId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        using var searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");
        foreach (ManagementObject obj in searcher.Get())
        {
            return obj["CommandLine"]?.ToString();
        }

        return null;
    }

    private static string? ExtractArguments(string commandLine, string executablePath)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        var trimmed = commandLine.Trim();
        if (trimmed.StartsWith('"'))
        {
            var closingQuote = trimmed.IndexOf('"', 1);
            if (closingQuote >= 0 && closingQuote + 1 < trimmed.Length)
            {
                return trimmed[(closingQuote + 1)..].TrimStart();
            }
        }

        if (trimmed.StartsWith(executablePath, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[executablePath.Length..].TrimStart();
        }

        var firstSpace = trimmed.IndexOf(' ');
        return firstSpace >= 0 && firstSpace + 1 < trimmed.Length
            ? trimmed[(firstSpace + 1)..].TrimStart()
            : null;
    }
}
