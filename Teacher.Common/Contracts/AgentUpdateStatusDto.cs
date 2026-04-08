namespace Teacher.Common.Contracts;

public sealed record AgentUpdateStatusDto(
    AgentUpdateStateKind State,
    string CurrentVersion,
    string? AvailableVersion,
    bool UpdateAvailable,
    DateTime? LastCheckedUtc,
    string? Message,
    bool RollbackPerformed = false,
    DateTime? LastUpdatedUtc = null);
