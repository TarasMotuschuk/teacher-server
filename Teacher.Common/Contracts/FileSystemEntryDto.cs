namespace Teacher.Common.Contracts;

public sealed record FileSystemEntryDto(
    string Name,
    string FullPath,
    bool IsDirectory,
    long? Size,
    DateTime LastModifiedUtc)
{
    public string AttributesDisplay { get; init; } = string.Empty;

    public string DisplayNameWithIcon => $"{TypeIcon} {Name}";

    public string Extension => IsDirectory ? string.Empty : Path.GetExtension(Name);

    public string SizeDisplay => FormatSize(Size);

    public string TypeIcon => IsDirectory ? "📁" : "📄";

    private static string FormatSize(long? size)
    {
        if (size is null)
        {
            return string.Empty;
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = size.Value;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{value:0} {units[unitIndex]}"
            : $"{value:0.##} {units[unitIndex]}";
    }
}
