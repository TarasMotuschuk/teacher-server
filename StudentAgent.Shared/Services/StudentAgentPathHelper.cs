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
            TryGrantUsersModifyAccess(root);
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
        => Path.Combine(GetRootDirectory(), "logs");

    public static string GetUpdatesDirectory()
        => Path.Combine(GetRootDirectory(), "updates");

    public static string GetUpdateStagingDirectory()
        => Path.Combine(GetUpdatesDirectory(), "staging");

    public static string GetUpdateBackupDirectory()
        => Path.Combine(GetUpdatesDirectory(), "backup");

    public static string GetUpdateStatusPath()
        => Path.Combine(GetUpdatesDirectory(), "update-status.json");

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
