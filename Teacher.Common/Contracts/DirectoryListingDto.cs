namespace Teacher.Common.Contracts;

public sealed record DirectoryListingDto(
    string CurrentPath,
    string? ParentPath,
    IReadOnlyList<FileSystemEntryDto> Entries);
