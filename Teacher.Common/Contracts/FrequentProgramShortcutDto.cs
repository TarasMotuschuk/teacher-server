namespace Teacher.Common.Contracts;

public sealed record FrequentProgramShortcutDto(
    string DisplayName,
    string CommandText,
    string ShortcutPath,
    string? TargetPath,
    string? Arguments);
