namespace Teacher.Common.Contracts;

public sealed record UpdateInfoDto(
    string CurrentVersion,
    string? AvailableVersion,
    bool UpdateAvailable,
    string? PackageUrl,
    string? PackageSha256,
    string? Message);
