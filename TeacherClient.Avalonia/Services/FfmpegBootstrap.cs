using FFmpeg.AutoGen;

namespace TeacherClient.CrossPlatform.Services;

public static class FfmpegBootstrap
{
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

    public static void TryConfigureBundledLibraries()
    {
        var dir = TryGetBundledFfmpegLibDirectory();
        if (dir is not null)
        {
            ffmpeg.RootPath = dir;
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
}
