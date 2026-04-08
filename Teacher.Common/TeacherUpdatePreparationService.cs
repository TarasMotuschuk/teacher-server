using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Teacher.Common.Contracts;

namespace Teacher.Common;

public sealed class TeacherUpdatePreparationService : IDisposable
{
    public const string DefaultManifestUrl = "https://github.com/TarasMotuschuk/teacher-server/releases/latest/download/student-agent-version.json";

    private readonly HttpClient _httpClient = new();
    private readonly string _rootDirectory;
    private readonly TeacherHostedUpdatePackageServer _packageServer;
    private readonly string _manifestUrl;
    private readonly string _manualDirectory;
    private readonly string _preparedStatePath;

    public TeacherUpdatePreparationService(string rootDirectory, string? manifestUrl = null, int hostedPort = 5199)
    {
        _rootDirectory = rootDirectory;
        _manifestUrl = string.IsNullOrWhiteSpace(manifestUrl) ? DefaultManifestUrl : manifestUrl.Trim();
        _manualDirectory = Path.Combine(_rootDirectory, "manual");
        _preparedStatePath = Path.Combine(_rootDirectory, "prepared-update.json");
        _packageServer = new TeacherHostedUpdatePackageServer(Path.Combine(_rootDirectory, "cache"), hostedPort);

        Directory.CreateDirectory(_rootDirectory);
        Directory.CreateDirectory(_manualDirectory);
    }

    public string ManifestUrl => _manifestUrl;

    public string ManualDirectory => _manualDirectory;

    public TeacherPreparedUpdateInfo? GetPreparedUpdate()
    {
        if (!File.Exists(_preparedStatePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_preparedStatePath);
            var prepared = JsonSerializer.Deserialize<TeacherPreparedUpdateInfo>(json);
            if (prepared is null || string.IsNullOrWhiteSpace(prepared.LocalZipPath) || !File.Exists(prepared.LocalZipPath))
            {
                return null;
            }

            return prepared;
        }
        catch
        {
            return null;
        }
    }

    public async Task<TeacherUpdateCheckResult> CheckForUpdateAsync(IProgress<TeacherUpdatePreparationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Report(progress, TeacherUpdatePreparationStage.Checking, null, "Checking update sources...", 0, null);

        var manualManifestPath = Path.Combine(_manualDirectory, "student-agent-version.json");
        if (File.Exists(manualManifestPath))
        {
            Report(progress, TeacherUpdatePreparationStage.Checking, null, $"Found manual manifest at '{manualManifestPath}'.", null, null);
            var manifest = await ReadManifestAsync(manualManifestPath, isLocalFile: true, cancellationToken);
            var localPackagePath = ResolveManualPackagePath(manifest, manualManifestPath);
            if (!File.Exists(localPackagePath))
            {
                throw new FileNotFoundException($"Manual update package was not found at '{localPackagePath}'.", localPackagePath);
            }

            Report(progress, TeacherUpdatePreparationStage.ReadyToDownload, manifest.Version, $"Manual update {manifest.Version} is ready to prepare.", 100, null);
            return new TeacherUpdateCheckResult(
                manifest.Version,
                manifest.Sha256,
                localPackagePath,
                null,
                IsManualSource: true,
                $"Manual update package found in '{_manualDirectory}'.");
        }

        Report(progress, TeacherUpdatePreparationStage.Checking, null, $"Downloading manifest from '{_manifestUrl}'.", null, null);
        var remoteManifest = await ReadManifestAsync(_manifestUrl, isLocalFile: false, cancellationToken);
        Report(progress, TeacherUpdatePreparationStage.ReadyToDownload, remoteManifest.Version, $"Remote update {remoteManifest.Version} is available.", 100, null);

        return new TeacherUpdateCheckResult(
            remoteManifest.Version,
            remoteManifest.Sha256,
            null,
            remoteManifest.Url,
            IsManualSource: false,
            $"Remote manifest loaded from '{_manifestUrl}'.");
    }

    public async Task<TeacherPreparedUpdateInfo> DownloadOrPrepareAsync(TeacherUpdateCheckResult checkResult, IProgress<TeacherUpdatePreparationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var versionDirectory = Path.Combine(_rootDirectory, "cache", checkResult.Version);
        Directory.CreateDirectory(versionDirectory);
        var destinationPath = Path.Combine(versionDirectory, "student-agent-update.zip");

        if (checkResult.IsManualSource)
        {
            if (string.IsNullOrWhiteSpace(checkResult.LocalPackagePath))
            {
                throw new InvalidOperationException("Manual package path is missing.");
            }

            Report(progress, TeacherUpdatePreparationStage.Downloading, checkResult.Version, "Preparing update from the manual package...", 0, null);
            await CopyWithProgressAsync(checkResult.LocalPackagePath, destinationPath, progress, checkResult.Version, cancellationToken);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(checkResult.PackageUrl))
            {
                throw new InvalidOperationException("Remote package URL is missing.");
            }

            Report(progress, TeacherUpdatePreparationStage.Downloading, checkResult.Version, $"Downloading update package {checkResult.Version}...", 0, null);
            await DownloadWithProgressAsync(checkResult.PackageUrl, destinationPath, progress, checkResult.Version, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(checkResult.PackageSha256))
        {
            ValidateSha256(destinationPath, checkResult.PackageSha256);
            Report(progress, TeacherUpdatePreparationStage.Downloading, checkResult.Version, "Package checksum verified.", 100, null);
        }

        var prepared = new TeacherPreparedUpdateInfo(
            checkResult.Version,
            destinationPath,
            checkResult.PackageSha256,
            DateTime.UtcNow,
            checkResult.IsManualSource);

        File.WriteAllText(_preparedStatePath, JsonSerializer.Serialize(prepared, new JsonSerializerOptions
        {
            WriteIndented = true,
        }));

        Report(progress, TeacherUpdatePreparationStage.Prepared, checkResult.Version, $"Update {checkResult.Version} is prepared and ready for deployment.", 100, null);
        return prepared;
    }

    public async Task<PreferredUpdateSourceDto> BuildPreferredSourceForAgentAsync(string agentAddress, TeacherPreparedUpdateInfo preparedUpdate, CancellationToken cancellationToken = default)
    {
        var hosted = await _packageServer.PrepareLocalPackageAsync(
            preparedUpdate.Version,
            preparedUpdate.LocalZipPath,
            preparedUpdate.PackageSha256,
            agentAddress,
            cancellationToken);

        return new PreferredUpdateSourceDto(
            hosted.Version,
            hosted.HostedPackageUrl,
            hosted.PackageSha256);
    }

    public void Dispose()
    {
        _packageServer.Dispose();
        _httpClient.Dispose();
    }

    private static void Report(IProgress<TeacherUpdatePreparationProgress>? progress, TeacherUpdatePreparationStage stage, string? version, string message, int? percent, long? bytesTransferred = null, long? totalBytes = null)
        => progress?.Report(new TeacherUpdatePreparationProgress(stage, version, message, percent, bytesTransferred, totalBytes));

    private static void ValidateSha256(string packagePath, string expectedSha256)
    {
        using var stream = File.OpenRead(packagePath);
        var hash = SHA256.HashData(stream);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();
        if (!string.Equals(actual, expectedSha256.Trim().ToLowerInvariant(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Prepared update package checksum does not match the manifest.");
        }
    }

    private async Task<TeacherUpdateManifest> ReadManifestAsync(string source, bool isLocalFile, CancellationToken cancellationToken)
    {
        TeacherUpdateManifest? manifest;
        if (isLocalFile)
        {
            var json = await File.ReadAllTextAsync(source, cancellationToken);
            manifest = JsonSerializer.Deserialize<TeacherUpdateManifest>(json);
        }
        else
        {
            manifest = await _httpClient.GetFromJsonAsync<TeacherUpdateManifest>(source, cancellationToken);
        }

        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new InvalidOperationException("Update manifest is missing the version field.");
        }

        return manifest;
    }

    private string ResolveManualPackagePath(TeacherUpdateManifest manifest, string manualManifestPath)
    {
        var manualManifestDirectory = Path.GetDirectoryName(manualManifestPath)!;
        if (!string.IsNullOrWhiteSpace(manifest.Url))
        {
            if (Uri.TryCreate(manifest.Url, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                return uri.LocalPath;
            }

            if (!Uri.TryCreate(manifest.Url, UriKind.Absolute, out _))
            {
                return Path.GetFullPath(Path.Combine(manualManifestDirectory, manifest.Url));
            }
        }

        var versionSpecific = Path.Combine(manualManifestDirectory, $"student-agent-update-{manifest.Version}.zip");
        if (File.Exists(versionSpecific))
        {
            return versionSpecific;
        }

        return Path.Combine(manualManifestDirectory, "student-agent-update.zip");
    }

    private async Task DownloadWithProgressAsync(string packageUrl, string destinationPath, IProgress<TeacherUpdatePreparationProgress>? progress, string version, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(packageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = File.Create(destinationPath);
        await CopyStreamWithProgressAsync(source, destination, progress, version, totalBytes, cancellationToken);
    }

    private async Task CopyWithProgressAsync(string sourcePath, string destinationPath, IProgress<TeacherUpdatePreparationProgress>? progress, string version, CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(sourcePath);
        await using var source = File.OpenRead(sourcePath);
        await using var destination = File.Create(destinationPath);
        await CopyStreamWithProgressAsync(source, destination, progress, version, fileInfo.Length, cancellationToken);
    }

    private static async Task CopyStreamWithProgressAsync(Stream source, Stream destination, IProgress<TeacherUpdatePreparationProgress>? progress, string version, long? totalBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long transferred = 0;
        var lastReportedPercent = -1;
        var stopwatch = Stopwatch.StartNew();
        long lastReportedMilliseconds = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            transferred += read;

            int? percent = totalBytes.HasValue && totalBytes > 0
                ? (int)Math.Min(100, transferred * 100 / totalBytes.Value)
                : null;
            var reportNow = transferred == totalBytes;

            if (percent.HasValue)
            {
                reportNow |= percent.Value >= 100 || percent.Value > lastReportedPercent;
            }
            else
            {
                reportNow |= stopwatch.ElapsedMilliseconds - lastReportedMilliseconds >= 250;
            }

            if (reportNow)
            {
                Report(progress, TeacherUpdatePreparationStage.Downloading, version, "Preparing update package...", percent, transferred, totalBytes);
                lastReportedPercent = percent ?? lastReportedPercent;
                lastReportedMilliseconds = stopwatch.ElapsedMilliseconds;
            }
        }

        if (totalBytes.HasValue && totalBytes.Value > 0 && lastReportedPercent < 100)
        {
            Report(progress, TeacherUpdatePreparationStage.Downloading, version, "Preparing update package...", 100, transferred, totalBytes);
        }
    }
}
