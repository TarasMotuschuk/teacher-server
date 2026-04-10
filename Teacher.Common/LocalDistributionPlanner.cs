using Teacher.Common.Contracts;

namespace Teacher.Common;

public static class LocalDistributionPlanner
{
    public static LocalDistributionPlan Build(FileSystemEntryDto entry, string destinationRoot)
    {
        var normalizedDestinationRoot = RemoteWindowsPath.Normalize(destinationRoot);

        if (!entry.IsDirectory)
        {
            return new LocalDistributionPlan(
                entry.Name,
                normalizedDestinationRoot,
                [],
                [new LocalDistributionFile(entry.FullPath, normalizedDestinationRoot, entry.Name)]);
        }

        var remoteRoot = RemoteWindowsPath.Combine(normalizedDestinationRoot, entry.Name);
        var directories = new List<string> { remoteRoot };
        var files = new List<LocalDistributionFile>();

        foreach (var directory in Directory.EnumerateDirectories(entry.FullPath, "*", SearchOption.AllDirectories))
        {
            var relativeDirectory = Path.GetRelativePath(entry.FullPath, directory);
            directories.Add(RemoteWindowsPath.CombineSegments(remoteRoot, relativeDirectory));
        }

        foreach (var filePath in Directory.EnumerateFiles(entry.FullPath, "*", SearchOption.AllDirectories))
        {
            var relativeFilePath = Path.GetRelativePath(entry.FullPath, filePath)
                .Replace(Path.DirectorySeparatorChar, '\\')
                .Replace(Path.AltDirectorySeparatorChar, '\\');
            var relativeDirectory = Path.GetDirectoryName(relativeFilePath)?
                .Replace(Path.DirectorySeparatorChar, '\\')
                .Replace(Path.AltDirectorySeparatorChar, '\\');
            var remoteDirectory = string.IsNullOrWhiteSpace(relativeDirectory)
                ? remoteRoot
                : RemoteWindowsPath.CombineSegments(remoteRoot, relativeDirectory);

            files.Add(new LocalDistributionFile(filePath, remoteDirectory, $"{entry.Name}\\{relativeFilePath}"));
        }

        return new LocalDistributionPlan(entry.Name, normalizedDestinationRoot, directories, files);
    }
}
