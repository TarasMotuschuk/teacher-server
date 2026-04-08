using Teacher.Common.Contracts;

internal sealed record UpdaterStatusFile(
    AgentUpdateStateKind State,
    string TargetVersion,
    string Message,
    bool RollbackPerformed,
    DateTime UpdatedAtUtc);
