using System.Text.Json;
using StudentAgent.Services;
using Teacher.Common.Contracts;

namespace StudentAgent.Service.Services;

public sealed class DesktopIconLayoutService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly AgentLogService _logService;
    private readonly string _uiHostPath;

    public DesktopIconLayoutService(AgentLogService logService)
    {
        _logService = logService;
        _uiHostPath = Path.Combine(AppContext.BaseDirectory, "StudentAgent.UIHost.exe");
    }

    public IReadOnlyList<DesktopIconLayoutSummaryDto> GetLayouts()
    {
        var directory = StudentAgentPathHelper.GetDesktopLayoutsDirectory();
        Directory.CreateDirectory(directory);

        return Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(Path.GetDirectoryName(path), StudentAgentPathHelper.GetDesktopLayoutResultsDirectory(), StringComparison.OrdinalIgnoreCase))
            .Select(TryReadSummary)
            .OfType<DesktopIconLayoutSummaryDto>()
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public DesktopIconLayoutOperationResultDto SaveLayout(string? layoutName)
        => Execute("save", layoutName);

    public DesktopIconLayoutOperationResultDto RestoreLayout(string? layoutName)
        => Execute("restore", layoutName);

    public DesktopIconLayoutSnapshotDto GetLayout(string? layoutName)
    {
        var normalizedLayoutName = StudentAgentPathHelper.SanitizeLayoutName(layoutName);
        var path = StudentAgentPathHelper.GetDesktopLayoutFilePath(normalizedLayoutName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Desktop icon layout '{normalizedLayoutName}' was not found.", path);
        }

        var snapshot = JsonSerializer.Deserialize<DesktopIconLayoutSnapshotDto>(File.ReadAllText(path), JsonOptions);
        return snapshot ?? throw new InvalidOperationException("Desktop icon layout file is invalid.");
    }

    public DesktopIconLayoutOperationResultDto ApplyLayout(ApplyDesktopIconLayoutRequest request)
    {
        if (request.Layout is null)
        {
            throw new InvalidOperationException("Desktop icon layout payload is missing.");
        }

        var targetLayoutName = StudentAgentPathHelper.SanitizeLayoutName(request.TargetLayoutName ?? request.Layout.Name);
        var layoutPath = StudentAgentPathHelper.GetDesktopLayoutFilePath(targetLayoutName);
        Directory.CreateDirectory(Path.GetDirectoryName(layoutPath)!);

        var snapshot = request.Layout with
        {
            Name = targetLayoutName,
            SavedAtUtc = DateTime.UtcNow,
        };

        File.WriteAllText(layoutPath, JsonSerializer.Serialize(snapshot, JsonOptions));
        _logService.LogInfo($"Applied desktop icon layout '{targetLayoutName}' with {snapshot.Icons.Count} icons to local storage.");

        if (!request.RestoreAfterApply)
        {
            return new DesktopIconLayoutOperationResultDto(
                targetLayoutName,
                snapshot.Icons.Count,
                snapshot.SavedAtUtc,
                $"Desktop icon layout '{targetLayoutName}' stored.");
        }

        return Execute("restore", targetLayoutName);
    }

    private static DesktopIconLayoutSummaryDto? TryReadSummary(string path)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<DesktopIconLayoutSnapshotDto>(File.ReadAllText(path), JsonOptions);
            return snapshot is null
                ? null
                : new DesktopIconLayoutSummaryDto(snapshot.Name, snapshot.SavedAtUtc, snapshot.Icons.Count);
        }
        catch
        {
            return null;
        }
    }

    private static void TryDeleteResultFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore temp file cleanup failures.
        }
    }

    private static string QuoteArgument(string value)
        => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private DesktopIconLayoutOperationResultDto Execute(string operation, string? layoutName)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Desktop icon layouts are only supported on Windows.");
        }

        if (!File.Exists(_uiHostPath))
        {
            throw new FileNotFoundException("StudentAgent.UIHost.exe was not found.", _uiHostPath);
        }

        var sessionId = SessionProcessLauncher.GetActiveSessionId();
        if (sessionId < 0)
        {
            throw new InvalidOperationException("No active interactive session is available.");
        }

        var normalizedLayoutName = StudentAgentPathHelper.SanitizeLayoutName(layoutName);
        var resultsDirectory = StudentAgentPathHelper.GetDesktopLayoutResultsDirectory();
        Directory.CreateDirectory(resultsDirectory);

        var resultPath = Path.Combine(
            resultsDirectory,
            $"{operation}-{normalizedLayoutName}-{DateTime.UtcNow:yyyyMMddHHmmssfff}.json");

        try
        {
            var arguments = string.Join(' ', [
                "desktop-icons",
                operation,
                QuoteArgument(normalizedLayoutName),
                QuoteArgument(resultPath)
            ]);

            var exitCode = SessionProcessLauncher.StartProcessInSessionAndWait(
                _uiHostPath,
                arguments,
                sessionId,
                hideWindow: true,
                timeout: TimeSpan.FromSeconds(30));

            if (!File.Exists(resultPath))
            {
                throw new InvalidOperationException($"Desktop icon {operation} did not produce a result file.");
            }

            var result = JsonSerializer.Deserialize<DesktopIconCommandResultDto>(File.ReadAllText(resultPath), JsonOptions)
                ?? throw new InvalidOperationException("Desktop icon command returned no result.");

            if (exitCode != 0 || !result.Succeeded)
            {
                throw new InvalidOperationException(result.Error ?? $"Desktop icon {operation} failed.");
            }

            _logService.LogInfo($"Desktop icon layout '{normalizedLayoutName}' {operation} completed in session {sessionId}.");
            var message = operation == "save"
                ? $"Desktop icon layout '{normalizedLayoutName}' saved."
                : $"Desktop icon layout '{normalizedLayoutName}' restored.";

            return new DesktopIconLayoutOperationResultDto(
                result.LayoutName,
                result.IconCount,
                result.UpdatedAtUtc,
                message);
        }
        finally
        {
            TryDeleteResultFile(resultPath);
        }
    }
}
