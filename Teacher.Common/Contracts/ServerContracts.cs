namespace Teacher.Common.Contracts;

public sealed record ServerInfoDto(
    string MachineName,
    string CurrentUser,
    string OsDescription,
    DateTime ServerTimeUtc,
    bool IsVisibleModeEnabled,
    bool IsBrowserLockEnabled);

public sealed record BrowserLockStateRequest(bool Enabled);
