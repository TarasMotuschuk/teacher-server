namespace Teacher.Common.Contracts;

public sealed record DesktopIconCommandResultDto(
    bool Succeeded,
    string LayoutName,
    int IconCount,
    DateTime UpdatedAtUtc,
    string? Error = null);
