using Teacher.Common.Contracts;

namespace Teacher.Common;

public static class LocalPathEntryFactory
{
    public static FileSystemEntryDto CreateFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.", nameof(path));
        }

        path = Path.GetFullPath(path);
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException("The path does not exist.", path);
        }

        var isDirectory = Directory.Exists(path);
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        long? size = isDirectory ? null : new FileInfo(path).Length;
        var lastMod = isDirectory ? Directory.GetLastWriteTimeUtc(path) : File.GetLastWriteTimeUtc(path);
        return new FileSystemEntryDto(name, path, isDirectory, size, lastMod);
    }
}
