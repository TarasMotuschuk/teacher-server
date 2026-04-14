using FFmpeg.AutoGen;

namespace TeacherClient.CrossPlatform.Services;

public static class FfmpegBootstrap
{
    /// <summary>
    /// Returns the directory that contains FFmpeg shared libraries (DLLs / dylibs) when bundled with the app,
    /// or <c>null</c> if none are present. Pass this to <c>SIPSorceryMedia.FFmpeg.FFmpegInit.Initialise(..., libPath, ...)</c>;
    /// that API only searches PATH or a fixed <c>FFmpeg/bin/x64</c> layout when <c>libPath</c> is null.
    /// </summary>
    public static string? TryGetBundledFfmpegLibDirectory()
    {
        try
        {
            // Windows publishes next to the exe. macOS app bundles publish into Contents/MacOS.
            var baseDir = AppContext.BaseDirectory;

            var candidates = new[]
            {
                Path.Combine(baseDir, "ffmpeg"),
                Path.Combine(baseDir, "ffmpeg", "bin"),
                // macOS: put dylibs into Contents/Frameworks/ffmpeg
                Path.GetFullPath(Path.Combine(baseDir, "..", "Frameworks", "ffmpeg")),
            };

            foreach (var dir in candidates)
            {
                if (Directory.Exists(dir) && LooksLikeFfmpegLibDirectory(dir))
                {
                    return dir;
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

    private static bool LooksLikeFfmpegLibDirectory(string dir)
    {
        try
        {
            // Minimal check: the two core libs SIPSorcery will load.
            if (OperatingSystem.IsWindows())
            {
                return Directory.EnumerateFiles(dir, "avcodec*.dll").Any()
                       && Directory.EnumerateFiles(dir, "avutil*.dll").Any();
            }

            return Directory.EnumerateFiles(dir, "libavcodec*.dylib").Any()
                   && Directory.EnumerateFiles(dir, "libavutil*.dylib").Any();
        }
        catch
        {
            return false;
        }
    }
}
