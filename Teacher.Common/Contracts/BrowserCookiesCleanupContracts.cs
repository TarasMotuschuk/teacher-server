namespace Teacher.Common.Contracts;

public sealed record BrowserCookiesCleanupRequest();

public sealed record BrowserCookiesCleanupResultDto(
    bool Success,
    string Message,
    IReadOnlyList<string> StoppedProcesses,
    IReadOnlyList<string> ClearedItems,
    IReadOnlyList<string> Errors);

