using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace Teacher.Common;

public sealed class TeacherClientUpdateService : IDisposable
{
    public const string DefaultManifestUrl = "https://github.com/TarasMotuschuk/teacher-server/releases/latest/download/classcommander-client-version.json";

    private readonly HttpClient _httpClient = new();
    private readonly string _rootDirectory;
    private readonly string _downloadsDirectory;
    private readonly string _statePath;
    private readonly string _manifestUrl;
    private readonly string _currentVersion;

    public TeacherClientUpdateService(string rootDirectory, string currentVersion, string? manifestUrl = null)
    {
        _rootDirectory = rootDirectory;
        _downloadsDirectory = Path.Combine(_rootDirectory, "downloads");
        _statePath = Path.Combine(_rootDirectory, "client-installer.json");
        _manifestUrl = string.IsNullOrWhiteSpace(manifestUrl) ? DefaultManifestUrl : manifestUrl.Trim();
        _currentVersion = NormalizeVersionString(currentVersion);

        Directory.CreateDirectory(_rootDirectory);
        Directory.CreateDirectory(_downloadsDirectory);
    }

    public string ManifestUrl => _manifestUrl;

    public string CurrentVersion => _currentVersion;

    public string DownloadsDirectory => _downloadsDirectory;

    public TeacherClientInstallerInfo? GetReadyInstaller()
    {
        if (!File.Exists(_statePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_statePath);
            var state = JsonSerializer.Deserialize<TeacherClientInstallerInfo>(json);
            if (state is null || string.IsNullOrWhiteSpace(state.LocalInstallerPath) || !File.Exists(state.LocalInstallerPath))
            {
                return null;
            }

            return state;
        }
        catch
        {
            return null;
        }
    }

    public async Task<TeacherClientUpdateCheckResult> CheckForUpdateAsync(IProgress<TeacherClientUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Report(progress, TeacherClientUpdateStage.Checking, "Checking client update manifest...", 0, null, null);
        var manifest = await _httpClient.GetFromJsonAsync<TeacherClientUpdateManifest>(_manifestUrl, cancellationToken)
            ?? throw new InvalidOperationException("Client update manifest could not be read.");

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new InvalidOperationException("Client update manifest is missing the version field.");
        }

        var asset = ResolvePlatformAsset(manifest);
        var availableVersion = NormalizeVersionString(manifest.Version);
        if (!IsNewerVersion(availableVersion, _currentVersion))
        {
            Report(progress, TeacherClientUpdateStage.UpToDate, $"Client is already on version {_currentVersion}.", 100, null, null);
            return new TeacherClientUpdateCheckResult(
                _currentVersion,
                availableVersion,
                false,
                asset.PlatformLabel,
                asset.PackageUrl,
                asset.PackageSha256,
                asset.FileName,
                $"Already on version {_currentVersion}.");
        }

        Report(progress, TeacherClientUpdateStage.Available, $"Client update {availableVersion} is available for {asset.PlatformLabel}.", 100, null, null);
        return new TeacherClientUpdateCheckResult(
            _currentVersion,
            availableVersion,
            true,
            asset.PlatformLabel,
            asset.PackageUrl,
            asset.PackageSha256,
            asset.FileName,
            $"Update {availableVersion} is available for {asset.PlatformLabel}.");
    }

    public async Task<TeacherClientInstallerInfo> DownloadInstallerAsync(TeacherClientUpdateCheckResult checkResult, IProgress<TeacherClientUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(checkResult.PackageUrl) || string.IsNullOrWhiteSpace(checkResult.AssetFileName))
        {
            throw new InvalidOperationException("Client update package URL is missing.");
        }

        var versionDirectory = Path.Combine(_downloadsDirectory, checkResult.AvailableVersion);
        Directory.CreateDirectory(versionDirectory);
        var destinationPath = Path.Combine(versionDirectory, checkResult.AssetFileName);

        Report(progress, TeacherClientUpdateStage.Downloading, $"Downloading {checkResult.PlatformLabel} installer {checkResult.AvailableVersion}...", 0, null, null);
        await DownloadWithProgressAsync(checkResult.PackageUrl, destinationPath, progress, cancellationToken);

        if (!string.IsNullOrWhiteSpace(checkResult.PackageSha256))
        {
            ValidateSha256(destinationPath, checkResult.PackageSha256);
            Report(progress, TeacherClientUpdateStage.Downloading, "Installer checksum verified.", 100, null, null);
        }

        var installerInfo = new TeacherClientInstallerInfo(
            checkResult.AvailableVersion,
            destinationPath,
            checkResult.PlatformLabel,
            DateTime.UtcNow);

        File.WriteAllText(_statePath, JsonSerializer.Serialize(installerInfo, new JsonSerializerOptions
        {
            WriteIndented = true,
        }));

        Report(progress, TeacherClientUpdateStage.ReadyToInstall, $"Installer {checkResult.AssetFileName} is ready.", 100, null, null);
        return installerInfo;
    }

    public void LaunchInstaller(TeacherClientInstallerInfo installerInfo)
    {
        if (!File.Exists(installerInfo.LocalInstallerPath))
        {
            throw new FileNotFoundException("Downloaded installer was not found.", installerInfo.LocalInstallerPath);
        }

        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = installerInfo.LocalInstallerPath,
                UseShellExecute = true,
            });
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                UseShellExecute = false,
                ArgumentList = { installerInfo.LocalInstallerPath },
            });
            return;
        }

        throw new PlatformNotSupportedException("Client installer launching is only supported on Windows and macOS.");
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private static void Report(IProgress<TeacherClientUpdateProgress>? progress, TeacherClientUpdateStage stage, string message, int? percent, long? bytesTransferred, long? totalBytes)
        => progress?.Report(new TeacherClientUpdateProgress(stage, message, percent, bytesTransferred, totalBytes));

    private static void ValidateSha256(string filePath, string expectedSha256)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();
        if (!string.Equals(actual, expectedSha256.Trim().ToLowerInvariant(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Downloaded installer checksum does not match the manifest.");
        }
    }

    private static TeacherClientPlatformAsset ResolvePlatformAsset(TeacherClientUpdateManifest manifest)
    {
        if (OperatingSystem.IsWindows())
        {
            if (string.IsNullOrWhiteSpace(manifest.WindowsMsiUrl))
            {
                throw new PlatformNotSupportedException("The client update manifest does not provide a Windows MSI asset.");
            }

            return new TeacherClientPlatformAsset(
                "Windows MSI",
                manifest.WindowsMsiUrl,
                manifest.WindowsMsiSha256,
                GetFileNameFromUrl(manifest.WindowsMsiUrl, $"ClassCommander-{manifest.Version}.msi"));
        }

        if (OperatingSystem.IsMacOS())
        {
            if (string.IsNullOrWhiteSpace(manifest.MacPkgUrl))
            {
                throw new PlatformNotSupportedException("The client update manifest does not provide a macOS PKG asset.");
            }

            return new TeacherClientPlatformAsset(
                "macOS PKG",
                manifest.MacPkgUrl,
                manifest.MacPkgSha256,
                GetFileNameFromUrl(manifest.MacPkgUrl, $"ClassCommander-{manifest.Version}.pkg"));
        }

        throw new PlatformNotSupportedException("Client updates are currently supported only on Windows and macOS.");
    }

    private static string GetFileNameFromUrl(string url, string fallback)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var fileName = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        return fallback;
    }

    private static bool IsNewerVersion(string candidate, string current)
    {
        if (Version.TryParse(candidate, out var candidateVersion) &&
            Version.TryParse(current, out var currentVersion))
        {
            return candidateVersion > currentVersion;
        }

        return !string.Equals(candidate, current, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeVersionString(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return "0.0.0";
        }

        if (!Version.TryParse(version.Trim(), out var parsed))
        {
            return version.Trim();
        }

        return parsed.Revision == 0
            ? $"{parsed.Major}.{parsed.Minor}.{parsed.Build}"
            : parsed.ToString();
    }

    private async Task DownloadWithProgressAsync(string packageUrl, string destinationPath, IProgress<TeacherClientUpdateProgress>? progress, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(packageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = File.Create(destinationPath);
        await CopyStreamWithProgressAsync(source, destination, progress, totalBytes, cancellationToken);
    }

    private static async Task CopyStreamWithProgressAsync(Stream source, Stream destination, IProgress<TeacherClientUpdateProgress>? progress, long? totalBytes, CancellationToken cancellationToken)
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
                Report(progress, TeacherClientUpdateStage.Downloading, "Downloading installer...", percent, transferred, totalBytes);
                lastReportedPercent = percent ?? lastReportedPercent;
                lastReportedMilliseconds = stopwatch.ElapsedMilliseconds;
            }
        }

        if (totalBytes.HasValue && totalBytes.Value > 0 && lastReportedPercent < 100)
        {
            Report(progress, TeacherClientUpdateStage.Downloading, "Downloading installer...", 100, transferred, totalBytes);
        }
    }
}
