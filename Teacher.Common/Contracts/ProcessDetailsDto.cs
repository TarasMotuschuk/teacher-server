namespace Teacher.Common.Contracts;

public sealed record ProcessDetailsDto(
    int Id,
    string Name,
    string? MainWindowTitle,
    string? ExecutablePath,
    string? CommandLine,
    long WorkingSetBytes,
    DateTime StartTimeUtc,
    bool HasVisibleWindow,
    bool Responding,
    int SessionId,
    int ThreadCount,
    int HandleCount,
    string? PriorityClass,
    TimeSpan TotalProcessorTime,
    string? FileVersion,
    string? ProductName,
    string? ErrorMessage);
