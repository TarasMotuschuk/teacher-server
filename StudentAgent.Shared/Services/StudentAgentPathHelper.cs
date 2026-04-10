using System.Security.AccessControl;
using System.Security.Principal;

namespace StudentAgent.Services;

internal static class StudentAgentPathHelper
{
    public static string GetRootDirectory()
    {
        var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(commonAppData))
        {
            var root = Path.Combine(commonAppData, "TeacherServer", "StudentAgent");
            EnsureDirectoryExists(root);
            return root;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            var root = Path.Combine(localAppData, "TeacherServer", "StudentAgent");
            EnsureDirectoryExists(root);
            return root;
        }

        var fallback = Path.Combine(AppContext.BaseDirectory, "data");
        EnsureDirectoryExists(fallback);
        return fallback;
    }

    public static string GetLogsDirectory()
    {
        var path = Path.Combine(GetRootDirectory(), "logs");
        EnsureDirectoryExists(path);
        TryGrantUsersModifyAccess(path);
        return path;
    }

    public static string GetUpdatesDirectory()
    {
        var path = Path.Combine(GetRootDirectory(), "updates");
        EnsureDirectoryExists(path);
        TryGrantUsersModifyAccess(path);
        return path;
    }

    public static string GetDesktopLayoutsDirectory()
    {
        var path = Path.Combine(GetRootDirectory(), "desktop-layouts");
        EnsureDirectoryExists(path);
        TryGrantUsersModifyAccess(path);
        return path;
    }

    public static string GetDesktopLayoutResultsDirectory()
        => Path.Combine(GetDesktopLayoutsDirectory(), "results");

    public static string GetDesktopLayoutFilePath(string layoutName)
        => Path.Combine(GetDesktopLayoutsDirectory(), $"{SanitizeLayoutName(layoutName)}.json");

    public static string GetUpdateStagingDirectory()
        => Path.Combine(GetUpdatesDirectory(), "staging");

    public static string GetUpdateBackupDirectory()
        => Path.Combine(GetUpdatesDirectory(), "backup");

    public static string GetUpdateRunnerDirectory()
        => Path.Combine(GetUpdatesDirectory(), "runner");

    public static string GetUpdateStatusPath()
        => Path.Combine(GetUpdatesDirectory(), "update-status.json");

    public static string SanitizeLayoutName(string? layoutName)
    {
        var normalized = string.IsNullOrWhiteSpace(layoutName)
            ? "default"
            : layoutName.Trim();

        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            normalized = normalized.Replace(invalidCharacter, '_');
        }

        return string.IsNullOrWhiteSpace(normalized) ? "default" : normalized;
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private static void TryGrantUsersModifyAccess(string root)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var directoryInfo = new DirectoryInfo(root);
            var security = directoryInfo.GetAccessControl();
            var usersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

            var hasRule = security.GetAccessRules(true, true, typeof(SecurityIdentifier))
                .OfType<FileSystemAccessRule>()
                .Any(rule =>
                    rule.IdentityReference == usersSid &&
                    rule.AccessControlType == AccessControlType.Allow &&
                    rule.FileSystemRights.HasFlag(FileSystemRights.Modify));

            if (hasRule)
            {
                return;
            }

            var rule = new FileSystemAccessRule(
                usersSid,
                FileSystemRights.Modify | FileSystemRights.Synchronize,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow);

            security.AddAccessRule(rule);
            directoryInfo.SetAccessControl(security);
        }
        catch
        {
            // If we cannot change ACLs in the current context, callers can still use the directory
            // when permissions are already sufficient.
        }
    }
}
