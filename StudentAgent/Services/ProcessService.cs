using System.Diagnostics;
using Teacher.Common.Contracts;

namespace StudentAgent.Services;

public sealed class ProcessService
{
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
