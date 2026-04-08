namespace Teacher.Common.Contracts;

public sealed record DriveSpaceDto(
    string RootPath,
    long TotalBytes,
    long FreeBytes,
    long AvailableBytes);
