using System.Reflection;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using SIPSorceryMedia.FFmpeg;

namespace TeacherClient.CrossPlatform.Services;

public static class FfmpegBootstrap
{
    /// <summary>
    /// Dylib filenames FFmpeg.AutoGen 7.0.x expects under <see cref="ffmpeg.RootPath"/> on macOS
    /// (<c>lib{shortName}.{version}.dylib</c>). Must stay aligned with the FFmpeg.AutoGen NuGet version
    /// referenced by SIPSorceryMedia.FFmpeg — do not read <c>ffmpeg.LibraryVersionMap</c> here (touching
    /// <see cref="ffmpeg"/> runs native init).
    /// </summary>
    private static readonly (string FileBase, int Version)[] MacFfmpegAutogen7Dylibs =
    {
        ("libavutil", 59),
        ("libavcodec", 61),
        ("libavformat", 61),
        ("libavdevice", 61),
        ("libavfilter", 10),
        ("libpostproc", 58),
        ("libswresample", 5),
        ("libswscale", 8),
    };

    private static readonly string[] MacCoreFfmpegPrefixes =
    {
        "libavutil", "libswresample", "libswscale", "libavcodec", "libavformat", "libavfilter", "libavdevice",
    };

    private static string? _macBundledLibDir;
    private static bool _macDllImportResolverRegistered;
    private static readonly Lock InitSync = new();
    private static bool _ffmpegInitialised;

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
    /// Optional: register a custom unmanaged-DLL resolver for the FFmpeg.AutoGen assembly. On macOS, the main
    /// FFmpeg native load path uses <c>dlopen</c> with full paths (see FFmpeg.AutoGen <c>MacFunctionResolver</c>),
    /// so this does not fix missing <c>libavutil.59.dylib</c> — but it can help edge P/Invokes that still use logical names.
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

    /// <summary>
    /// Explains macOS FFmpeg load failures: expected dylib names (FFmpeg 7 / AutoGen 7), files on disk, and
    /// <c>dlerror</c> when <c>libavutil</c> cannot be opened (often a missing transitive dylib).
    /// </summary>
    /// <param name="libDir">Bundled directory passed to FFmpeg init (contains <c>*.dylib</c>), or null.</param>
    /// <returns>Diagnostic text, or an empty string when not applicable.</returns>
    public static string BuildMacOsBundledFfmpegDiagnostics(string? libDir)
    {
        if (!OperatingSystem.IsMacOS() || string.IsNullOrEmpty(libDir) || !Directory.Exists(libDir))
        {
            return string.Empty;
        }

        static string ExpectedFileName(string fileBase, int ver) => $"{fileBase}.{ver}.dylib";

        var lines = new List<string>
        {
            "FFmpeg.AutoGen on macOS loads libraries with dlopen(fullPath) — not .NET DllImport — under RootPath.",
            "Expected FFmpeg 7-compatible dylib names (FFmpeg.AutoGen 7.0 / SIPSorceryMedia.FFmpeg 8.x):",
        };

        var missing = new List<string>();
        foreach (var (fileBase, ver) in MacFfmpegAutogen7Dylibs)
        {
            var name = ExpectedFileName(fileBase, ver);
            if (!File.Exists(Path.Combine(libDir, name)))
            {
                missing.Add(name);
            }
        }

        if (missing.Count > 0)
        {
            lines.Add($"Missing required filenames: {string.Join(", ", missing)}");
        }

        try
        {
            var found = Directory.GetFiles(libDir, "*.dylib").Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (found.Length > 0)
            {
                lines.Add("Dylibs in bundle directory:");
                foreach (var n in found)
                {
                    lines.Add($"  - {n}");
                }
            }
        }
        catch
        {
        }

        var avutilPath = Path.Combine(libDir, ExpectedFileName("libavutil", 59));
        if (File.Exists(avutilPath))
        {
            var err = TryGetMacOsDlOpenFailureReason(avutilPath);
            if (!string.IsNullOrEmpty(err))
            {
                lines.Add($"dlopen/libavutil: {err}");
            }
        }

        return string.Join(Environment.NewLine, lines);
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
    /// Performs one-time FFmpeg native bootstrap for the current process and returns the bundled library directory
    /// that was used for initialization, if any.
    /// </summary>
    public static string? EnsureInitialized()
    {
        lock (InitSync)
        {
            if (_ffmpegInitialised)
            {
                return TryGetBundledFfmpegLibDirectory();
            }

            RegisterMacOsFfmpegDllImportResolver();
            TryConfigureBundledLibraries();
            TryPreloadBundledFfmpegMacOS(includeAvDevice: true);

            var bundledLibDir = TryGetBundledFfmpegLibDirectory();
            FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_ERROR, bundledLibDir, null);
            _ffmpegInitialised = true;
            return bundledLibDir;
        }
    }

    /// <summary>
    /// Prepares FFmpeg codec libraries for encode/decode use without touching capture/input-device registration.
    /// This is the preferred bootstrap for custom raw-frame pipelines that should avoid loading <c>avdevice</c>.
    /// </summary>
    /// <returns>Directory containing native FFmpeg libraries, or <c>null</c>.</returns>
    public static string? EnsureEncoderOnlyConfigured()
    {
        lock (InitSync)
        {
            RegisterMacOsFfmpegDllImportResolver();
            TryConfigureBundledLibraries();
            TryPreloadBundledFfmpegMacOS(includeAvDevice: false);
            return TryGetBundledFfmpegLibDirectory();
        }
    }

    /// <summary>
    /// Preload bundled <c>*.dylib</c> in a sensible order so transitive dependencies (e.g. codec libs) are in memory
    /// before FFmpeg.AutoGen loads <c>libavutil</c> — avoids "Unable to load ... avutil.xx" when files exist but dyld
    /// resolves dependencies lazily.
    /// </summary>
    public static void TryPreloadBundledFfmpegMacOS(bool includeAvDevice = true)
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

        static bool IsCoreLib(string fileName, bool includeAvDevice)
        {
            foreach (var p in MacCoreFfmpegPrefixes)
            {
                if (!includeAvDevice && string.Equals(p, "libavdevice", StringComparison.Ordinal))
                {
                    continue;
                }

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
            if (!IsCoreLib(Path.GetFileName(path), includeAvDevice))
            {
                TryLoadOnce(path);
            }
        }

        foreach (var path in paths)
        {
            if (IsCoreLib(Path.GetFileName(path), includeAvDevice))
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

    private static string? TryGetMacOsDlOpenFailureReason(string dylibPath)
    {
        try
        {
            var h = NativeLibrary.Load(dylibPath);
            NativeLibrary.Free(h);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
