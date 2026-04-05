namespace Teacher.Common.Contracts;

public sealed record ServerInfoDto(
    string MachineName,
    string CurrentUser,
    string OsDescription,
    DateTime ServerTimeUtc,
    bool IsVisibleModeEnabled,
    bool IsBrowserLockEnabled,
    bool IsInputLockEnabled,
    string AgentVersion);

public sealed record BrowserLockStateRequest(bool Enabled);
public sealed record InputLockStateRequest(bool Enabled);
public sealed record StudentPolicySettingsRequest(
    int DesktopIconAutoRestoreMinutes,
    int BrowserLockCheckIntervalSeconds);

public enum RemoteCommandRunAs
{
    CurrentUser = 0,
    Administrator = 1
}

public sealed record RemoteCommandRequest(
    string Script,
    RemoteCommandRunAs RunAs);

public sealed record FrequentProgramShortcutDto(
    string DisplayName,
    string CommandText,
    string ShortcutPath,
    string? TargetPath,
    string? Arguments);

public enum PowerActionKind
{
    Shutdown = 0,
    Restart = 1,
    LogOff = 2
}

public sealed record PowerActionRequest(PowerActionKind Action);
