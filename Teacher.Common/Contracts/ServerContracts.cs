namespace Teacher.Common.Contracts;

public sealed record ServerInfoDto(
    string MachineName,
    string CurrentUser,
    string OsDescription,
    DateTime ServerTimeUtc,
    bool IsVisibleModeEnabled,
    bool IsBrowserLockEnabled,
    bool IsInputLockEnabled);

public sealed record BrowserLockStateRequest(bool Enabled);
public sealed record InputLockStateRequest(bool Enabled);

public enum PowerActionKind
{
    Shutdown = 0,
    Restart = 1,
    LogOff = 2
}

public sealed record PowerActionRequest(PowerActionKind Action);
