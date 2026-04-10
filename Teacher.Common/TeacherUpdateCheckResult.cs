namespace Teacher.Common;

public sealed record TeacherUpdateCheckResult(
    string Version,
    string? PackageSha256,
    string? LocalPackagePath,
    string? PackageUrl,
    bool IsManualSource,
    string SourceDescription);
