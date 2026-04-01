using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Teacher.Common.Contracts;

namespace StudentAgent.Services;

public sealed class AgentUpdateService
{
    public const string ServiceName = "StudentAgentService";

    private readonly object _sync = new();
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AgentLogService _logService;
    private readonly string _manifestUrl;
    private readonly string _currentVersion;
    private AgentUpdateStatusDto _status;
    private Task? _runningTask;

    public AgentUpdateService(IHttpClientFactory httpClientFactory, AgentLogService logService, IOptions<AgentOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _logService = logService;
        _manifestUrl = options.Value.UpdateManifestUrl?.Trim() ?? string.Empty;
        _currentVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";
        _status = new AgentUpdateStatusDto(
            AgentUpdateStateKind.Idle,
            _currentVersion,
            null,
            false,
            null,
            string.IsNullOrWhiteSpace(_manifestUrl) ? "Update manifest URL is not configured." : null);
    }

    public AgentUpdateStatusDto GetStatus()
    {
        lock (_sync)
        {
            return _status with { };
        }
    }

    public async Task<UpdateInfoDto> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        UpdateState(AgentUpdateStateKind.Checking, null, false, "Checking for updates...", DateTime.UtcNow);

        try
        {
            var manifest = await LoadManifestAsync(cancellationToken);
            var updateAvailable = IsVersionGreater(manifest.Version, _currentVersion);
            var message = updateAvailable
                ? $"Update {manifest.Version} is available."
                : "Agent is up to date.";

            UpdateState(
                updateAvailable ? AgentUpdateStateKind.Available : AgentUpdateStateKind.UpToDate,
                manifest.Version,
                updateAvailable,
                message,
                DateTime.UtcNow);

            return new UpdateInfoDto(
                _currentVersion,
                manifest.Version,
                updateAvailable,
                manifest.Url,
                manifest.Sha256,
                message);
        }
        catch (Exception ex)
        {
            UpdateState(AgentUpdateStateKind.Failed, null, false, ex.Message, DateTime.UtcNow);
            throw;
        }
    }

    public Task<AgentUpdateStatusDto> StartUpdateAsync(bool checkForUpdatesFirst, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_runningTask is { IsCompleted: false })
            {
                return Task.FromResult(_status with { });
            }

            _runningTask = RunUpdateAsync(checkForUpdatesFirst, cancellationToken);
            return Task.FromResult(_status with { });
        }
    }

    private async Task RunUpdateAsync(bool checkForUpdatesFirst, CancellationToken cancellationToken)
    {
        try
        {
            var updateInfo = checkForUpdatesFirst
                ? await CheckForUpdatesAsync(cancellationToken)
                : await CheckForUpdatesAsync(cancellationToken);

            if (!updateInfo.UpdateAvailable || string.IsNullOrWhiteSpace(updateInfo.PackageUrl))
            {
                UpdateState(AgentUpdateStateKind.UpToDate, updateInfo.AvailableVersion, false, updateInfo.Message, DateTime.UtcNow);
                return;
            }

            UpdateState(AgentUpdateStateKind.Downloading, updateInfo.AvailableVersion, true, "Downloading update package...", DateTime.UtcNow);
            Directory.CreateDirectory(StudentAgentPathHelper.GetUpdatesDirectory());
            var packageFileName = $"student-agent-{updateInfo.AvailableVersion}.zip";
            var packagePath = Path.Combine(StudentAgentPathHelper.GetUpdatesDirectory(), packageFileName);
            await DownloadPackageAsync(updateInfo.PackageUrl, packagePath, cancellationToken);

            if (!string.IsNullOrWhiteSpace(updateInfo.PackageSha256))
            {
                ValidateSha256(packagePath, updateInfo.PackageSha256);
            }

            var updaterPath = Path.Combine(AppContext.BaseDirectory, "StudentAgent.Updater.exe");
            if (!File.Exists(updaterPath))
            {
                throw new InvalidOperationException($"Updater binary was not found at '{updaterPath}'.");
            }

            UpdateState(AgentUpdateStateKind.Installing, updateInfo.AvailableVersion, true, "Launching updater...", DateTime.UtcNow);
            LaunchUpdater(updaterPath, packagePath, updateInfo.AvailableVersion!);
            _logService.LogInfo($"Launching StudentAgent updater for version {updateInfo.AvailableVersion}.");
        }
        catch (Exception ex)
        {
            _logService.LogError($"Agent update failed: {ex}");
            UpdateState(AgentUpdateStateKind.Failed, _status.AvailableVersion, _status.UpdateAvailable, ex.Message, DateTime.UtcNow);
        }
    }

    private async Task<AgentUpdateManifest> LoadManifestAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_manifestUrl))
        {
            throw new InvalidOperationException("Update manifest URL is not configured.");
        }

        var httpClient = _httpClientFactory.CreateClient(nameof(AgentUpdateService));
        var manifest = await httpClient.GetFromJsonAsync<AgentUpdateManifest>(_manifestUrl, cancellationToken);
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version) || string.IsNullOrWhiteSpace(manifest.Url))
        {
            throw new InvalidOperationException("Update manifest is missing required fields.");
        }

        return manifest;
    }

    private async Task DownloadPackageAsync(string packageUrl, string destinationPath, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(nameof(AgentUpdateService));
        await using var source = await httpClient.GetStreamAsync(packageUrl, cancellationToken);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken);
    }

    private static bool IsVersionGreater(string candidate, string current)
    {
        if (!Version.TryParse(candidate, out var candidateVersion))
        {
            return false;
        }

        if (!Version.TryParse(current, out var currentVersion))
        {
            return true;
        }

        return candidateVersion > currentVersion;
    }

    private static void ValidateSha256(string packagePath, string expectedSha256)
    {
        using var stream = File.OpenRead(packagePath);
        var hash = SHA256.HashData(stream);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();
        var normalizedExpected = expectedSha256.Trim().ToLowerInvariant();
        if (!string.Equals(actual, normalizedExpected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Downloaded update package checksum does not match the manifest.");
        }
    }

    private void LaunchUpdater(string updaterPath, string packagePath, string targetVersion)
    {
        var installDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var args = string.Join(' ', [
            "--service-name", Quote(ServiceName),
            "--zip", Quote(packagePath),
            "--install-dir", Quote(installDirectory),
            "--backup-dir", Quote(StudentAgentPathHelper.GetUpdateBackupDirectory()),
            "--target-version", Quote(targetVersion)
        ]);

        var startInfo = new ProcessStartInfo
        {
            FileName = updaterPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = installDirectory
        };

        Process.Start(startInfo);
    }

    private void UpdateState(
        AgentUpdateStateKind state,
        string? availableVersion,
        bool updateAvailable,
        string? message,
        DateTime? lastCheckedUtc)
    {
        lock (_sync)
        {
            _status = new AgentUpdateStatusDto(
                state,
                _currentVersion,
                availableVersion,
                updateAvailable,
                lastCheckedUtc ?? _status.LastCheckedUtc,
                message);
        }
    }

    private static string Quote(string value)
        => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private sealed record AgentUpdateManifest(
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("sha256")] string? Sha256);
}
