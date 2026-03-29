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
