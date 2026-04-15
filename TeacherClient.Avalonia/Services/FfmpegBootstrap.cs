using System.Reflection;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace TeacherClient.CrossPlatform.Services;

public static class FfmpegBootstrap
{
    private static readonly string[] MacCoreFfmpegPrefixes =
    {
        "libavutil", "libswresample", "libswscale", "libavcodec", "libavformat", "libavfilter", "libavdevice",
    };

    private static string? _macBundledLibDir;
    private static bool _macDllImportResolverRegistered;

    /// <summary>
    /// Returns the directory that contains FFmpeg shared libraries (DLLs / dylibs) when bundled with the app,
    /// or <c>null</c> if none are present. Pass this to <c>SIPSorceryMedia.FFmpeg.FFmpegInit.Initialise(..., libPath, ...)</c>;
    /// that API only searches PATH or a fixed <c>FFmpeg/bin/x64</c> layout when <c>libPath</c> is null.
    /// </summary>
    /// <returns>Directory containing native FFmpeg libraries, or <c>null</c>.</returns>
    public static string? TryGetBundledFfmpegLibDirectory()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var roots = new[]
            {
                Path.Combine(baseDir, "ffmpeg", "lib"),
                Path.Combine(baseDir, "ffmpeg"),
                Path.Combine(baseDir, "ffmpeg", "bin"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "Frameworks", "ffmpeg", "lib")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "Frameworks", "ffmpeg")),
            };

            foreach (var root in roots)
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                if (OperatingSystem.IsWindows())
                {
                    if (LooksLikeFfmpegLibDirectoryWindows(root))
                    {
                        return root;
                    }

                    continue;
                }

                if (OperatingSystem.IsMacOS())
                {
                    var dir = FindMacOsFfmpegLibDirectory(root);
                    if (dir is not null)
                    {
                        return dir;
                    }

                    continue;
                }

                // Linux / other Unix (.so next to the app)
                if (Directory.EnumerateFiles(root, "libavcodec.so*", SearchOption.TopDirectoryOnly).Any()
                    && Directory.EnumerateFiles(root, "libavutil.so*", SearchOption.TopDirectoryOnly).Any())
                {
                    return root;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    /// <summary>
    /// Must run before any use of <see cref="ffmpeg"/> on macOS. FFmpeg.AutoGen asks the runtime to load
    /// logical names like <c>avutil.59</c>; the on-disk file is <c>libavutil.59.dylib</c>. This resolver maps
    /// <c>lib{libraryName}.dylib</c> under the bundled directory.
    /// </summary>
    public static void RegisterMacOsFfmpegDllImportResolver()
    {
        if (!OperatingSystem.IsMacOS() || _macDllImportResolverRegistered)
        {
            return;
        }

        var dir = TryGetBundledFfmpegLibDirectory();
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        _macBundledLibDir = dir;

        try
        {
            var asm = Assembly.Load("FFmpeg.AutoGen");
            NativeLibrary.SetDllImportResolver(asm, MacFfmpegDllImportResolver);
            _macDllImportResolverRegistered = true;
        }
        catch
        {
        }
    }

    public static void TryConfigureBundledLibraries()
    {
        var dir = TryGetBundledFfmpegLibDirectory();
        if (dir is not null)
        {
            ffmpeg.RootPath = dir;
        }
    }

    /// <summary>
    /// Preload bundled <c>*.dylib</c> in a sensible order so transitive dependencies (e.g. codec libs) are in memory
    /// before FFmpeg.AutoGen loads <c>libavutil</c> — avoids "Unable to load ... avutil.xx" when files exist but dyld
    /// resolves dependencies lazily.
    /// </summary>
    public static void TryPreloadBundledFfmpegMacOS()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var dir = TryGetBundledFfmpegLibDirectory();
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        var paths = Directory.GetFiles(dir, "*.dylib");
        if (paths.Length == 0)
        {
            return;
        }

        static bool IsCoreLib(string fileName)
        {
            foreach (var p in MacCoreFfmpegPrefixes)
            {
                if (fileName.StartsWith(p, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        Array.Sort(paths, StringComparer.Ordinal);
        foreach (var path in paths)
        {
            if (!IsCoreLib(Path.GetFileName(path)))
            {
                TryLoadOnce(path);
            }
        }

        foreach (var path in paths)
        {
            if (IsCoreLib(Path.GetFileName(path)))
            {
                TryLoadOnce(path);
            }
        }

        static void TryLoadOnce(string path)
        {
            try
            {
                NativeLibrary.Load(path);
            }
            catch
            {
            }
        }
    }

    private static string? FindMacOsFfmpegLibDirectory(string root)
    {
        foreach (var codecPath in Directory.EnumerateFiles(root, "libavcodec*.dylib", SearchOption.AllDirectories))
        {
            var dir = Path.GetDirectoryName(codecPath);
            if (dir is null)
            {
                continue;
            }

            if (Directory.GetFiles(dir, "libavutil*.dylib").Length > 0)
            {
                return dir;
            }
        }

        return null;
    }

    private static bool LooksLikeFfmpegLibDirectoryWindows(string dir)
    {
        return Directory.EnumerateFiles(dir, "avcodec*.dll").Any()
               && Directory.EnumerateFiles(dir, "avutil*.dll").Any();
    }

    private static IntPtr MacFfmpegDllImportResolver(string libraryName, Assembly? assembly, DllImportSearchPath? searchPath)
    {
        if (string.IsNullOrEmpty(_macBundledLibDir))
        {
            return IntPtr.Zero;
        }

        var path = Path.Combine(_macBundledLibDir, $"lib{libraryName}.dylib");
        if (!File.Exists(path))
        {
            return IntPtr.Zero;
        }

        try
        {
            return NativeLibrary.Load(path);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }
}
