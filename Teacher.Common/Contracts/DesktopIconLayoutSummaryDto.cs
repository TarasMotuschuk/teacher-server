namespace Teacher.Common.Contracts;

public sealed record DesktopIconLayoutSummaryDto(
    string Name,
    DateTime SavedAtUtc,
    int IconCount);
