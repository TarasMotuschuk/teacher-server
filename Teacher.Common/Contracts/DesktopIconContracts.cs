namespace Teacher.Common.Contracts;

public sealed record DesktopIconEntryDto(
    string Name,
    int X,
    int Y);

public sealed record DesktopIconLayoutSnapshotDto(
    string Name,
    DateTime SavedAtUtc,
    IReadOnlyList<DesktopIconEntryDto> Icons);

public sealed record DesktopIconLayoutSummaryDto(
    string Name,
    DateTime SavedAtUtc,
    int IconCount);

public sealed record SaveDesktopIconLayoutRequest(string? LayoutName = null);

public sealed record RestoreDesktopIconLayoutRequest(string? LayoutName = null);

public sealed record DesktopIconLayoutOperationResultDto(
    string LayoutName,
    int IconCount,
    DateTime UpdatedAtUtc,
    string Message);

public sealed record DesktopIconCommandResultDto(
    bool Succeeded,
    string LayoutName,
    int IconCount,
    DateTime UpdatedAtUtc,
    string? Error = null);

public sealed record ApplyDesktopIconLayoutRequest(
    DesktopIconLayoutSnapshotDto Layout,
    string? TargetLayoutName = null,
    bool RestoreAfterApply = true);
