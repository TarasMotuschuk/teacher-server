namespace Teacher.Common.Contracts;

public sealed record DemoSessionStatusDto(
    bool Active,
    string? SessionId,
    DateTime? StartedUtc,
    bool FullscreenLock);
