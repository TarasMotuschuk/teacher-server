namespace Teacher.Common.Contracts;

public sealed record ApplyDesktopIconLayoutRequest(
    DesktopIconLayoutSnapshotDto Layout,
    string? TargetLayoutName = null,
    bool RestoreAfterApply = true);
