namespace Teacher.Common.Contracts;

public sealed record DesktopIconLayoutSnapshotDto(
    string Name,
    DateTime SavedAtUtc,
    IReadOnlyList<DesktopIconEntryDto> Icons);
