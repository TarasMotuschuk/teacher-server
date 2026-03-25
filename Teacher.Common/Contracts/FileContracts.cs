namespace Teacher.Common.Contracts;

public sealed record FileSystemEntryDto(
    string Name,
    string FullPath,
    bool IsDirectory,
    long? Size,
    DateTime LastModifiedUtc);

public sealed record DirectoryListingDto(
    string CurrentPath,
    string? ParentPath,
    IReadOnlyList<FileSystemEntryDto> Entries);

public sealed record DeleteEntryRequest(string FullPath);

public sealed record CreateDirectoryRequest(string ParentPath, string Name);

public sealed record UploadFileMetadata(string DestinationDirectory, string FileName);
