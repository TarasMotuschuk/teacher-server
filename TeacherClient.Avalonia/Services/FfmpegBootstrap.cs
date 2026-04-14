using FFmpeg.AutoGen;

namespace TeacherClient.CrossPlatform.Services;

public static class FfmpegBootstrap
{
    public static void TryConfigureBundledLibraries()
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
                    ffmpeg.RootPath = dir;
                    return;
                }
            }
        }
        catch
        {
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
