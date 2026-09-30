using System.Diagnostics;

namespace TeacherClient.CrossPlatform.Services;

internal static class TestPlatformHost
{
    public const string DefaultLocalUrl = "http://127.0.0.1:5050";

    public static bool TryStart(out string? error)
    {
        error = null;
        var executable = ResolveExecutable();
        if (executable is not null)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable),
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            return true;
        }

        var projectPath = ResolveProjectPath();
        if (projectPath is not null)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{projectPath}\"",
                WorkingDirectory = Path.GetDirectoryName(projectPath),
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            return true;
        }

        error = "ClassCommander.TestPlatform";
        return false;
    }

    private static string? ResolveExecutable()
    {
        const string projectName = "ClassCommander.TestPlatform";
        var fileName = OperatingSystem.IsWindows() ? $"{projectName}.exe" : projectName;
        foreach (var candidate in EnumerateSearchRoots())
        {
            var path = Path.Combine(candidate, fileName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static string? ResolveProjectPath()
    {
        const string projectName = "ClassCommander.TestPlatform";
        foreach (var root in EnumerateRepositoryRoots())
        {
            var projectPath = Path.Combine(root, projectName, $"{projectName}.csproj");
            if (File.Exists(projectPath))
            {
                return projectPath;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSearchRoots()
    {
        const string projectName = "ClassCommander.TestPlatform";
        foreach (var root in EnumerateRepositoryRoots())
        {
            yield return Path.Combine(root, projectName, "bin", "Debug", "net10.0");
            yield return Path.Combine(root, projectName, "bin", "Release", "net10.0");
        }

        var baseDir = AppContext.BaseDirectory;
        yield return baseDir;
        yield return Path.Combine(baseDir, projectName);
        yield return Path.Combine(baseDir, "TestPlatform");

        var parentDir = Directory.GetParent(baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))?.FullName;
        if (parentDir is not null)
        {
            yield return Path.Combine(parentDir, projectName);
            yield return Path.Combine(parentDir, "TestPlatform");
        }
    }

    private static IEnumerable<string> EnumerateRepositoryRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (!seen.Add(dir.FullName))
            {
                continue;
            }

            if (File.Exists(Path.Combine(dir.FullName, "TeacherServer.sln")))
            {
                yield return dir.FullName;
            }
        }
    }
}
