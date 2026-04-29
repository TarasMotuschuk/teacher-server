namespace Teacher.Common.Contracts;

public sealed record BrowserCleanupRequest(
    bool ClearHistory,
    bool ClearCache);

public sealed record BrowserCleanupResultDto(
    bool Success,
    string Message,
    IReadOnlyList<string> StoppedProcesses,
    IReadOnlyList<string> ClearedItems,
    IReadOnlyList<string> Errors);

