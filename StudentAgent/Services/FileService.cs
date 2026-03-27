using Teacher.Common.Contracts;
using System.Security.AccessControl;
using System.Security.Principal;

namespace StudentAgent.Services;

public sealed class FileService
{
    public DirectoryListingDto GetDirectory(string path)
    {
        var directory = ResolveDirectory(path);
        var info = new DirectoryInfo(directory);

        var entries = info.EnumerateFileSystemInfos()
            .OrderByDescending(x => (x.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(MapEntry)
            .ToList();

        return new DirectoryListingDto(
            info.FullName,
            info.Parent?.FullName,
            entries);
    }

    public IEnumerable<string> GetRoots()
    {
        return Directory.GetLogicalDrives();
    }

    public void DeleteEntry(string fullPath)
    {
        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
            return;
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return;
        }

        throw new FileNotFoundException("Entry not found.", fullPath);
    }

    public void CreateDirectory(string parentPath, string name)
    {
        var safeName = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Folder name is required.", nameof(name))
            : name.Trim();

        Directory.CreateDirectory(Path.Combine(ResolveDirectory(parentPath), safeName));
    }

    public void ClearDirectoryContents(string fullPath)
    {
        var directory = new DirectoryInfo(ResolveDirectory(fullPath));
        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException($"Directory not found: {fullPath}");
        }

        foreach (var childDirectory in directory.EnumerateDirectories())
        {
            childDirectory.Delete(recursive: true);
        }

        foreach (var file in directory.EnumerateFiles())
        {
            file.Delete();
        }
    }

    public void EnsureSharedWritableDirectory(string fullPath)
    {
        var directoryPath = ResolveDirectory(fullPath);
        var directory = Directory.CreateDirectory(directoryPath);

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var security = directory.GetAccessControl();
            var everyoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            var accessRule = new FileSystemAccessRule(
                everyoneSid,
                FileSystemRights.CreateFiles |
                FileSystemRights.CreateDirectories |
                FileSystemRights.Write |
                FileSystemRights.ReadAndExecute |
                FileSystemRights.Modify |
                FileSystemRights.DeleteSubdirectoriesAndFiles,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow);

            security.ModifyAccessRule(AccessControlModification.Add, accessRule, out _);
            directory.SetAccessControl(security);
        }
        catch
        {
            // Best-effort ACL setup; the directory itself should still exist.
        }
    }

    public async Task SaveFileAsync(string destinationDirectory, string fileName, Stream source, CancellationToken cancellationToken)
    {
        var destinationPath = Path.Combine(ResolveDirectory(destinationDirectory), Path.GetFileName(fileName));
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken);
    }

    public (string FileName, Stream Stream, string ContentType) OpenRead(string fullPath)
    {
        var fileInfo = new FileInfo(fullPath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("File not found.", fullPath);
        }

        return (fileInfo.Name, fileInfo.OpenRead(), "application/octet-stream");
    }

    private static string ResolveDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Directory.GetLogicalDrives().First();
        }

        return Path.GetFullPath(path);
    }

    private static FileSystemEntryDto MapEntry(FileSystemInfo info)
    {
        var isDirectory = (info.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
        long? size = info is FileInfo fileInfo ? fileInfo.Length : null;

        return new FileSystemEntryDto(
            info.Name,
            info.FullName,
            isDirectory,
            size,
            info.LastWriteTimeUtc);
    }
}
