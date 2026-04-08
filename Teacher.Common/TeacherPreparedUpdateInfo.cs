namespace Teacher.Common;

public sealed record TeacherPreparedUpdateInfo(
    string Version,
    string LocalZipPath,
    string? PackageSha256,
    DateTime PreparedAtUtc,
    bool IsManualSource);
