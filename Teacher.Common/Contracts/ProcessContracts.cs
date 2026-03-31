namespace Teacher.Common.Contracts;

public sealed record ProcessInfoDto(
    int Id,
    string Name,
    string? MainWindowTitle,
    long WorkingSetBytes,
    DateTime StartTimeUtc,
    bool HasVisibleWindow);

public sealed record KillProcessRequest(int ProcessId);

public sealed record RestartProcessRequest(int ProcessId);

public sealed record OpenRemoteEntryRequest(string FullPath);

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
