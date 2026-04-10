namespace Teacher.Common.Contracts;

public sealed record DesktopIconLayoutOperationResultDto(
    string LayoutName,
    int IconCount,
    DateTime UpdatedAtUtc,
    string Message);
