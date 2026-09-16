#pragma warning disable SA1402

namespace ClassCommander.Testing.Core.Packaging;

public sealed record TestPackageManifestDto(
    string PackageFormat,
    int PackageFormatVersion,
    DateTime CreatedAtUtc,
    TestPackageIdentityDto Test,
    IReadOnlyList<TestPackageFileDto> Files,
    TestPackageSourceDto? Source);

public sealed record TestPackageIdentityDto(
    string PublicId,
    int Version,
    string Title);

public sealed record TestPackageFileDto(
    string Path,
    string Sha256);

public sealed record TestPackageSourceDto(
    string Kind,
    IReadOnlyList<string> IncludedFiles);

#pragma warning restore SA1402
