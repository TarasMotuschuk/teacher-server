using System.IO.Compression;
using System.Security.Cryptography;
using ClassCommander.Testing.Core.Serialization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using Teacher.Common.Contracts.Testing;

namespace ClassCommander.Testing.Core.Packaging;

public sealed class CctestPackageService
{
    public async Task SaveAsync(
        string packagePath,
        TestDefinitionDto definition,
        string? assetsSourceDirectory,
        string? originalImportPath = null,
        CancellationToken cancellationToken = default)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ClassCommander", "cctest-build", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var assetsDir = Path.Combine(tempRoot, "assets");
            Directory.CreateDirectory(assetsDir);
            var rewrittenAssets = new List<QuestionAssetDto>();

            foreach (var asset in definition.Assets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourcePath = ResolveAssetSourcePath(asset.Path, assetsSourceDirectory);
                if (sourcePath is null || !File.Exists(sourcePath))
                {
                    throw new FileNotFoundException($"Asset file was not found for '{asset.Id}'.", asset.Path);
                }

                var packagedName = $"{asset.Id}.webp";
                var packagedAbsolute = Path.Combine(assetsDir, packagedName);
                await ConvertToWebpAsync(sourcePath, packagedAbsolute, cancellationToken);
                rewrittenAssets.Add(asset with
                {
                    MimeType = "image/webp",
                    Path = $"assets/{packagedName}",
                });
            }

            var packagedDefinition = definition with { Assets = rewrittenAssets };
            var definitionJson = TestPlatformJson.Serialize(packagedDefinition);
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "definition.json"), definitionJson, cancellationToken);

            var includedImports = new List<string>();
            if (!string.IsNullOrWhiteSpace(originalImportPath) && File.Exists(originalImportPath))
            {
                var importsDir = Path.Combine(tempRoot, "imports");
                Directory.CreateDirectory(importsDir);
                var dest = Path.Combine(importsDir, Path.GetFileName(originalImportPath));
                File.Copy(originalImportPath, dest, overwrite: true);
                includedImports.Add($"imports/{Path.GetFileName(originalImportPath)}");
            }

            var files = new List<TestPackageFileDto>
            {
                new("definition.json", await Sha256FileAsync(Path.Combine(tempRoot, "definition.json"), cancellationToken)),
            };
            foreach (var asset in rewrittenAssets)
            {
                var relative = asset.Path.Replace('/', Path.DirectorySeparatorChar);
                files.Add(new TestPackageFileDto(asset.Path, await Sha256FileAsync(Path.Combine(tempRoot, relative), cancellationToken)));
            }

            foreach (var importRelative in includedImports)
            {
                files.Add(new TestPackageFileDto(
                    importRelative,
                    await Sha256FileAsync(Path.Combine(tempRoot, importRelative.Replace('/', Path.DirectorySeparatorChar)), cancellationToken)));
            }

            var manifest = new TestPackageManifestDto(
                PackageFormat: "cctest",
                PackageFormatVersion: 1,
                CreatedAtUtc: DateTime.UtcNow,
                Test: new TestPackageIdentityDto(packagedDefinition.PublicId, packagedDefinition.Version, packagedDefinition.Title),
                Files: files,
                Source: includedImports.Count == 0
                    ? null
                    : new TestPackageSourceDto("mytest-xml-import", includedImports));
            await File.WriteAllTextAsync(
                Path.Combine(tempRoot, "manifest.json"),
                TestPlatformJson.Serialize(manifest),
                cancellationToken);

            var directory = Path.GetDirectoryName(packagePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(packagePath))
            {
                File.Delete(packagePath);
            }

            ZipFile.CreateFromDirectory(tempRoot, packagePath, CompressionLevel.Optimal, includeBaseDirectory: false);
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    public async Task<(TestPackageManifestDto Manifest, TestDefinitionDto Definition, string ExtractedDirectory)> OpenAsync(
        string packagePath,
        string? extractDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("Test package was not found.", packagePath);
        }

        var target = extractDirectory
            ?? Path.Combine(Path.GetTempPath(), "ClassCommander", "cctest-open", Guid.NewGuid().ToString("N"));
        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }

        Directory.CreateDirectory(target);
        ZipFile.ExtractToDirectory(packagePath, target);

        var manifestPath = Path.Combine(target, "manifest.json");
        var definitionPath = Path.Combine(target, "definition.json");
        if (!File.Exists(manifestPath) || !File.Exists(definitionPath))
        {
            throw new InvalidOperationException("Invalid .cctest package: manifest.json and definition.json are required.");
        }

        var manifest = TestPlatformJson.Deserialize<TestPackageManifestDto>(
            await File.ReadAllTextAsync(manifestPath, cancellationToken));
        var definition = TestPlatformJson.Deserialize<TestDefinitionDto>(
            await File.ReadAllTextAsync(definitionPath, cancellationToken));

        foreach (var file in manifest.Files)
        {
            var absolute = Path.Combine(target, file.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolute))
            {
                throw new InvalidOperationException($"Package is missing listed file '{file.Path}'.");
            }

            var actualHash = await Sha256FileAsync(absolute, cancellationToken);
            if (!string.Equals(actualHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Package integrity check failed for '{file.Path}'.");
            }
        }

        return (manifest, definition, target);
    }

    private static string? ResolveAssetSourcePath(string assetPath, string? assetsSourceDirectory)
    {
        if (Path.IsPathRooted(assetPath) && File.Exists(assetPath))
        {
            return assetPath;
        }

        if (!string.IsNullOrWhiteSpace(assetsSourceDirectory))
        {
            var candidate = Path.Combine(assetsSourceDirectory, Path.GetFileName(assetPath));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var relative = Path.Combine(assetsSourceDirectory, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(relative))
            {
                return relative;
            }

            // Working directory layout: .../assets/file
            var nested = Path.Combine(Path.GetDirectoryName(assetsSourceDirectory) ?? assetsSourceDirectory, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(nested))
            {
                return nested;
            }
        }

        return File.Exists(assetPath) ? assetPath : null;
    }

    private static async Task ConvertToWebpAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        await using var input = File.OpenRead(sourcePath);
        using var image = await Image.LoadAsync(input, cancellationToken);
        var encoder = new WebpEncoder { Quality = 90 };
        await image.SaveAsWebpAsync(destinationPath, encoder, cancellationToken);
    }

    private static async Task<string> Sha256FileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
