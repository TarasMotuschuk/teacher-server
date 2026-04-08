namespace Teacher.Common.Contracts;

public sealed record StudentPolicySettingsRequest(
    int DesktopIconAutoRestoreMinutes,
    int BrowserLockCheckIntervalSeconds);
