using Teacher.Common.Contracts;
using StudentAgent.Services;

namespace StudentAgent.Service.Services;

public sealed class PublicDesktopShortcutService
{
    private readonly AgentLogService _logService;

    public PublicDesktopShortcutService(AgentLogService logService)
    {
        _logService = logService;
    }

    public IReadOnlyList<FrequentProgramShortcutDto> GetPublicDesktopShortcuts()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var desktopPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));

        if (string.IsNullOrWhiteSpace(desktopPath) || !Directory.Exists(desktopPath))
        {
            return [];
        }

        var shortcuts = new List<FrequentProgramShortcutDto>();
        foreach (var shortcutPath in Directory.EnumerateFiles(desktopPath, "*.lnk", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var shortcut = ReadShortcut(shortcutPath);
                if (shortcut is null || string.IsNullOrWhiteSpace(shortcut.TargetPath))
                {
                    continue;
                }

                var commandText = BuildCommandText(shortcut.TargetPath, shortcut.Arguments);
                shortcuts.Add(new FrequentProgramShortcutDto(
                    Path.GetFileNameWithoutExtension(shortcutPath),
                    commandText,
                    shortcutPath,
                    shortcut.TargetPath,
                    shortcut.Arguments));
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Failed to read public desktop shortcut '{shortcutPath}': {ex.Message}");
            }
        }

        return shortcuts
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.CommandText, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ShortcutInfo? ReadShortcut(string shortcutPath)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell COM type is not available.");

        dynamic shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Failed to create WScript.Shell instance.");

        try
        {
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            return new ShortcutInfo(
                Convert.ToString(shortcut.TargetPath),
                Convert.ToString(shortcut.Arguments));
        }
        finally
        {
            try
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
            catch
            {
            }
        }
    }

    private static string BuildCommandText(string? targetPath, string? arguments)
    {
        var quotedTarget = QuoteArgument(targetPath ?? string.Empty);
        return string.IsNullOrWhiteSpace(arguments)
            ? quotedTarget
            : $"{quotedTarget} {arguments.Trim()}";
    }

    private static string QuoteArgument(string value)
        => value.Contains(' ', StringComparison.Ordinal) && !value.StartsWith('"')
            ? $"\"{value}\""
            : value;

    private sealed record ShortcutInfo(string? TargetPath, string? Arguments);
}
