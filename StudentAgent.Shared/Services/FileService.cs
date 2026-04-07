using System.Security.AccessControl;
using System.Security.Principal;
using Teacher.Common.Contracts;

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

    public DriveSpaceDto GetDriveSpace(string? path)
    {
        var resolvedPath = ResolveDirectory(path);
        var root = Path.GetPathRoot(resolvedPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException("Could not resolve the drive root.");
        }

        var drive = new DriveInfo(root);
        if (!drive.IsReady)
        {
            throw new IOException($"Drive '{root}' is not ready.");
        }

        return new DriveSpaceDto(
            drive.RootDirectory.FullName,
            drive.TotalSize,
            drive.TotalFreeSpace,
            drive.AvailableFreeSpace);
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

    public void RenameEntry(string fullPath, string newName)
    {
        var sourcePath = Path.GetFullPath(fullPath);
        var safeName = ValidateEntryName(newName);
        var parentDirectory = Path.GetDirectoryName(sourcePath);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            throw new InvalidOperationException("Cannot rename a root path.");
        }

        var destinationPath = Path.Combine(parentDirectory, safeName);
        if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Directory.Exists(destinationPath) || File.Exists(destinationPath))
        {
            throw new IOException($"An entry with the name '{safeName}' already exists.");
        }

        if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, destinationPath);
            return;
        }

        if (File.Exists(sourcePath))
        {
            File.Move(sourcePath, destinationPath);
            return;
        }

        throw new FileNotFoundException("Entry not found.", sourcePath);
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

    private static string ValidateEntryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Entry name is required.", nameof(name));
        }

        var trimmed = name.Trim();
        if (!string.Equals(trimmed, Path.GetFileName(trimmed), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only a file or folder name is allowed.");
        }

        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException("The file or folder name contains invalid characters.");
        }

        return trimmed;
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
            info.LastWriteTimeUtc)
        {
            AttributesDisplay = FormatAttributes(info.Attributes),
        };
    }

    private static string FormatAttributes(FileAttributes attributes)
    {
        var values = new List<string>();

        if ((attributes & FileAttributes.Directory) == FileAttributes.Directory)
        {
            values.Add("Dir");
        }

        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
        {
            values.Add("R");
        }

        if ((attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
        {
            values.Add("H");
        }

        if ((attributes & FileAttributes.System) == FileAttributes.System)
        {
            values.Add("S");
        }

        if ((attributes & FileAttributes.Archive) == FileAttributes.Archive)
        {
            values.Add("A");
        }

        return string.Join(", ", values);
    }
}
