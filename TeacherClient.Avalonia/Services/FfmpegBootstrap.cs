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
                // macOS: put dylibs into Contents/Frameworks/ffmpeg
                Path.GetFullPath(Path.Combine(baseDir, "..", "Frameworks", "ffmpeg")),
            };

            foreach (var dir in candidates)
            {
                if (Directory.Exists(dir))
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
}
