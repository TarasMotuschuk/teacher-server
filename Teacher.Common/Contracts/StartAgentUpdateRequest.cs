namespace Teacher.Common.Contracts;

public sealed record StartAgentUpdateRequest(
    bool CheckForUpdatesFirst = true,
    PreferredUpdateSourceDto? PreferredSource = null,
    bool FallbackToConfiguredManifest = true);
