using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Teacher.Common.Contracts;

namespace StudentAgent.Services;

public sealed class AgentUpdateService
{
    public const string ServiceName = "StudentAgentService";

    private readonly object _sync = new();
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHostApplicationLifetime _hostLifetime;
    private readonly AgentLogService _logService;
    private readonly string _manifestUrl;
    private readonly string _statusPath;
    private readonly string _currentVersion;
    private AgentUpdateStatusDto _status;
    private Task? _runningTask;

    public AgentUpdateService(
        IHttpClientFactory httpClientFactory,
        IHostApplicationLifetime hostLifetime,
        AgentLogService logService,
        IOptions<AgentOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _hostLifetime = hostLifetime;
        _logService = logService;
        _manifestUrl = options.Value.UpdateManifestUrl?.Trim() ?? string.Empty;
        _statusPath = StudentAgentPathHelper.GetUpdateStatusPath();
        _currentVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";
        _status = new AgentUpdateStatusDto(
            AgentUpdateStateKind.Idle,
            _currentVersion,
            null,
            false,
            null,
            string.IsNullOrWhiteSpace(_manifestUrl) ? "Update manifest URL is not configured." : null,
            false,
            null);
    }

    public AgentUpdateStatusDto GetStatus()
    {
        lock (_sync)
        {
            _status = MergePersistedStatus(_status);
            return _status with { };
        }
    }

    public async Task<UpdateInfoDto> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        UpdateState(AgentUpdateStateKind.Checking, null, false, "Checking for updates...", DateTime.UtcNow, false, null);

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
                DateTime.UtcNow,
                false,
                null);

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
            UpdateState(AgentUpdateStateKind.Failed, null, false, ex.Message, DateTime.UtcNow, false, null);
            throw;
        }
    }

    public Task<AgentUpdateStatusDto> StartUpdateAsync(StartAgentUpdateRequest request)
    {
        lock (_sync)
        {
            if (_runningTask is { IsCompleted: false })
            {
                return Task.FromResult(_status with { });
            }

            // Do not pass the HTTP request token: the handler returns 202 Accepted immediately and the
            // connection may close, canceling a long download with "A task was canceled."
            _runningTask = RunUpdateAsync(request, _hostLifetime.ApplicationStopping);
            return Task.FromResult(_status with { });
        }
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

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory, Func<string, bool> skipPredicate)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            if (skipPredicate(file))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, overwrite: true);
        }
    }

    private static string Quote(string value)
        => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private async Task RunUpdateAsync(StartAgentUpdateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updateInfo = request.PreferredSource is not null
                ? BuildPreferredUpdateInfo(request.PreferredSource)
                : await CheckForUpdatesAsync(cancellationToken);

            if (request.PreferredSource is not null)
            {
                UpdateState(
                    AgentUpdateStateKind.Available,
                    updateInfo.AvailableVersion,
                    updateInfo.UpdateAvailable,
                    "Using teacher-hosted update package.",
                    DateTime.UtcNow,
                    rollbackPerformed: false,
                    lastUpdatedUtc: null);
            }

            if (!updateInfo.UpdateAvailable || string.IsNullOrWhiteSpace(updateInfo.PackageUrl))
            {
                UpdateState(AgentUpdateStateKind.UpToDate, updateInfo.AvailableVersion, false, updateInfo.Message, DateTime.UtcNow, false, null);
                return;
            }

            UpdateState(AgentUpdateStateKind.Downloading, updateInfo.AvailableVersion, true, "Downloading update package...", DateTime.UtcNow, false, null);
            Directory.CreateDirectory(StudentAgentPathHelper.GetUpdatesDirectory());
            var packageFileName = $"student-agent-{updateInfo.AvailableVersion}.zip";
            var packagePath = Path.Combine(StudentAgentPathHelper.GetUpdatesDirectory(), packageFileName);
            try
            {
                await DownloadPackageAsync(updateInfo.PackageUrl, packagePath, cancellationToken);
            }
            catch (Exception preferredEx) when (request.PreferredSource is not null && request.FallbackToConfiguredManifest)
            {
                _logService.LogWarning($"Teacher-hosted update source failed, falling back to configured manifest: {preferredEx.Message}");
                updateInfo = await CheckForUpdatesAsync(cancellationToken);
                if (!updateInfo.UpdateAvailable || string.IsNullOrWhiteSpace(updateInfo.PackageUrl))
                {
                    UpdateState(AgentUpdateStateKind.UpToDate, updateInfo.AvailableVersion, false, updateInfo.Message, DateTime.UtcNow, false, null);
                    return;
                }

                UpdateState(AgentUpdateStateKind.Downloading, updateInfo.AvailableVersion, true, "Teacher-hosted source failed. Downloading from fallback manifest...", DateTime.UtcNow, false, null);
                await DownloadPackageAsync(updateInfo.PackageUrl, packagePath, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(updateInfo.PackageSha256))
            {
                ValidateSha256(packagePath, updateInfo.PackageSha256);
            }

            var updaterPath = Path.Combine(AppContext.BaseDirectory, "StudentAgent.Updater.exe");
            if (!File.Exists(updaterPath))
            {
                throw new InvalidOperationException($"Updater binary was not found at '{updaterPath}'.");
            }

            UpdateState(AgentUpdateStateKind.Installing, updateInfo.AvailableVersion, true, "Launching updater...", DateTime.UtcNow, false, null);
            LaunchUpdater(updaterPath, packagePath, updateInfo.AvailableVersion!);
            _logService.LogInfo($"Launching StudentAgent updater for version {updateInfo.AvailableVersion}.");
        }
        catch (Exception ex)
        {
            _logService.LogError($"Agent update failed: {ex}");
            UpdateState(AgentUpdateStateKind.Failed, _status.AvailableVersion, _status.UpdateAvailable, ex.Message, DateTime.UtcNow, false, DateTime.UtcNow);
        }
    }

    private UpdateInfoDto BuildPreferredUpdateInfo(PreferredUpdateSourceDto preferredSource)
    {
        var updateAvailable = IsVersionGreater(preferredSource.Version, _currentVersion);
        return new UpdateInfoDto(
            _currentVersion,
            preferredSource.Version,
            updateAvailable,
            preferredSource.PackageUrl,
            preferredSource.PackageSha256,
            updateAvailable ? $"Teacher-hosted update {preferredSource.Version} is available." : "Agent is up to date.");
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

    private void LaunchUpdater(string updaterPath, string packagePath, string targetVersion)
    {
        var installDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var runnerDirectory = PrepareUpdaterRunnerDirectory(installDirectory);
        var stagedUpdaterPath = Path.Combine(runnerDirectory, Path.GetFileName(updaterPath));

        var args = string.Join(' ', [
            "--service-name", Quote(ServiceName),
            "--zip", Quote(packagePath),
            "--install-dir", Quote(installDirectory),
            "--backup-dir", Quote(StudentAgentPathHelper.GetUpdateBackupDirectory()),
            "--target-version", Quote(targetVersion)
        ]);

        var startInfo = new ProcessStartInfo
        {
            FileName = stagedUpdaterPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = runnerDirectory,
        };

        Process.Start(startInfo);
    }

    private string PrepareUpdaterRunnerDirectory(string installDirectory)
    {
        var runnerRoot = StudentAgentPathHelper.GetUpdateRunnerDirectory();
        Directory.CreateDirectory(runnerRoot);
        CleanupStaleUpdaterRunnerDirectories(runnerRoot);

        var runnerDirectory = Path.Combine(runnerRoot, Guid.NewGuid().ToString("N"));
        CopyDirectory(installDirectory, runnerDirectory, static _ => false);
        return runnerDirectory;
    }

    private void CleanupStaleUpdaterRunnerDirectories(string runnerRoot)
    {
        foreach (var directory in Directory.GetDirectories(runnerRoot))
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Failed to remove stale updater runner directory '{directory}': {ex.Message}");
            }
        }
    }

    private void UpdateState(
        AgentUpdateStateKind state,
        string? availableVersion,
        bool updateAvailable,
        string? message,
        DateTime? lastCheckedUtc,
        bool rollbackPerformed,
        DateTime? lastUpdatedUtc)
    {
        lock (_sync)
        {
            _status = new AgentUpdateStatusDto(
                state,
                _currentVersion,
                availableVersion,
                updateAvailable,
                lastCheckedUtc ?? _status.LastCheckedUtc,
                message,
                rollbackPerformed,
                lastUpdatedUtc ?? _status.LastUpdatedUtc);
        }
    }

    private AgentUpdateStatusDto MergePersistedStatus(AgentUpdateStatusDto current)
    {
        if (!File.Exists(_statusPath))
        {
            return current;
        }

        try
        {
            var persisted = JsonSerializer.Deserialize<PersistedUpdateStatus>(File.ReadAllText(_statusPath));
            if (persisted is null)
            {
                return current;
            }

            return current with
            {
                State = persisted.State,
                AvailableVersion = persisted.TargetVersion,
                UpdateAvailable = persisted.State is AgentUpdateStateKind.Available or AgentUpdateStateKind.Downloading or AgentUpdateStateKind.Installing,
                Message = persisted.Message,
                RollbackPerformed = persisted.RollbackPerformed,
                LastUpdatedUtc = persisted.UpdatedAtUtc,
            };
        }
        catch
        {
            return current;
        }
    }

    private sealed record AgentUpdateManifest(
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("sha256")] string? Sha256);

    private sealed record PersistedUpdateStatus(
        AgentUpdateStateKind State,
        string TargetVersion,
        string Message,
        bool RollbackPerformed,
        DateTime UpdatedAtUtc);
}
