namespace Teacher.Common.Contracts;

public sealed record ProcessInfoDto(
    int Id,
    string Name,
    string? MainWindowTitle,
    long WorkingSetBytes,
    DateTime StartTimeUtc,
    bool HasVisibleWindow);
