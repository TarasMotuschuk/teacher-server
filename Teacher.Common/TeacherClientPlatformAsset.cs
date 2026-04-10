namespace Teacher.Common;

internal sealed record TeacherClientPlatformAsset(
    string PlatformLabel,
    string PackageUrl,
    string? PackageSha256,
    string FileName);
