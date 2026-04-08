namespace Teacher.Common.Contracts;

public sealed record PreferredUpdateSourceDto(
    string Version,
    string PackageUrl,
    string? PackageSha256);
