namespace Teacher.Common;

public sealed record TeacherClientUpdateCheckResult(
    string CurrentVersion,
    string AvailableVersion,
    bool UpdateAvailable,
    string PlatformLabel,
    string PackageUrl,
    string? PackageSha256,
    string AssetFileName,
    string Message);
