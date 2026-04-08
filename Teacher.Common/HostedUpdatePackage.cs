namespace Teacher.Common;

public sealed record HostedUpdatePackage(
    string Version,
    string HostedPackageUrl,
    string? PackageSha256,
    string LocalZipPath);
