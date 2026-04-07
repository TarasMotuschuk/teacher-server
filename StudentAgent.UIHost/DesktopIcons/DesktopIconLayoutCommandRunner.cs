using System.Text.Json;
using StudentAgent.Services;
using Teacher.Common.Contracts;

namespace StudentAgent.UIHost.DesktopIcons;

internal static class DesktopIconLayoutCommandRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static bool TryExecute(string[] args, AgentLogService logService, out DesktopIconCommandResultDto result, out string? resultPath)
    {
        result = default!;
        resultPath = null;

        if (args.Length != 4 || !string.Equals(args[0], "desktop-icons", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var operation = args[1];
        var layoutName = StudentAgentPathHelper.SanitizeLayoutName(args[2]);
        resultPath = args[3];

        try
        {
            result = string.Equals(operation, "save", StringComparison.OrdinalIgnoreCase)
                ? SaveLayout(layoutName, logService)
                : string.Equals(operation, "restore", StringComparison.OrdinalIgnoreCase)
                    ? RestoreLayout(layoutName, logService)
                    : throw new InvalidOperationException($"Unsupported desktop icon operation '{operation}'.");
        }
        catch (Exception ex)
        {
            logService.LogError($"Desktop icon command '{operation}' failed: {ex}");
            result = new DesktopIconCommandResultDto(
                Succeeded: false,
                LayoutName: layoutName,
                IconCount: 0,
                UpdatedAtUtc: DateTime.UtcNow,
                Error: ex.Message);
        }

        WriteResult(resultPath, result);
        return true;
    }

    private static DesktopIconCommandResultDto SaveLayout(string layoutName, AgentLogService logService)
    {
        var icons = DesktopListView.CaptureIcons()
            .Select(x => new DesktopIconEntryDto(x.Name, x.X, x.Y))
            .ToList();

        var snapshot = new DesktopIconLayoutSnapshotDto(layoutName, DateTime.UtcNow, icons);
        var layoutPath = StudentAgentPathHelper.GetDesktopLayoutFilePath(layoutName);
        Directory.CreateDirectory(Path.GetDirectoryName(layoutPath)!);
        File.WriteAllText(layoutPath, JsonSerializer.Serialize(snapshot, JsonOptions));

        logService.LogInfo($"Saved desktop icon layout '{layoutName}' with {icons.Count} icons.");
        return new DesktopIconCommandResultDto(
            Succeeded: true,
            LayoutName: layoutName,
            IconCount: icons.Count,
            UpdatedAtUtc: snapshot.SavedAtUtc);
    }

    private static DesktopIconCommandResultDto RestoreLayout(string layoutName, AgentLogService logService)
    {
        var layoutPath = StudentAgentPathHelper.GetDesktopLayoutFilePath(layoutName);
        if (!File.Exists(layoutPath))
        {
            throw new FileNotFoundException($"Desktop icon layout '{layoutName}' was not found.", layoutPath);
        }

        var snapshot = JsonSerializer.Deserialize<DesktopIconLayoutSnapshotDto>(File.ReadAllText(layoutPath), JsonOptions)
            ?? throw new InvalidOperationException("Desktop icon layout file is invalid.");

        var restoredCount = DesktopListView.RestoreIcons(snapshot.Icons
            .Select(x => new DesktopIconInfo(x.Name, x.X, x.Y))
            .ToList());

        logService.LogInfo($"Restored desktop icon layout '{layoutName}' with {restoredCount} matching icons.");
        return new DesktopIconCommandResultDto(
            Succeeded: true,
            LayoutName: layoutName,
            IconCount: restoredCount,
            UpdatedAtUtc: DateTime.UtcNow);
    }

    private static void WriteResult(string path, DesktopIconCommandResultDto result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(result, JsonOptions));
    }
}
