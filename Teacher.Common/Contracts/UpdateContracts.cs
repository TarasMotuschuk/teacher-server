namespace Teacher.Common.Contracts;

public enum AgentUpdateStateKind
{
    Idle = 0,
    Checking = 1,
    UpToDate = 2,
    Available = 3,
    Downloading = 4,
    Installing = 5,
    Succeeded = 6,
    Failed = 7,
    RolledBack = 8,
}

public sealed record UpdateInfoDto(
    string CurrentVersion,
    string? AvailableVersion,
    bool UpdateAvailable,
    string? PackageUrl,
    string? PackageSha256,
    string? Message);

public sealed record PreferredUpdateSourceDto(
    string Version,
    string PackageUrl,
    string? PackageSha256);

public sealed record AgentUpdateStatusDto(
    AgentUpdateStateKind State,
    string CurrentVersion,
    string? AvailableVersion,
    bool UpdateAvailable,
    DateTime? LastCheckedUtc,
    string? Message,
    bool RollbackPerformed = false,
    DateTime? LastUpdatedUtc = null);

public sealed record StartAgentUpdateRequest(
    bool CheckForUpdatesFirst = true,
    PreferredUpdateSourceDto? PreferredSource = null,
    bool FallbackToConfiguredManifest = true);
