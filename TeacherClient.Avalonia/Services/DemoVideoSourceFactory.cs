using System.Drawing;
using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.FFmpeg;

namespace TeacherClient.CrossPlatform.Services;

public sealed class DemoVideoSourceFactory
{
    public IVideoSource CreateSource(Rectangle captureArea, int captureFps, DemoDiagnosticLog diagnosticLog, string studentBaseUrl)
    {
        if (OperatingSystem.IsMacOS())
        {
            try
            {
                var raw = new MacOsRawScreenVideoSource(captureArea, captureFps);
                diagnosticLog.LogInfo($"Teacher demo capture source created for {studentBaseUrl}: MacOsRawScreenVideoSource raw=BGRA.");
                return raw;
            }
            catch (Exception ex)
            {
                diagnosticLog.LogWarning($"Teacher demo macOS raw capture backend failed for {studentBaseUrl}: {ex.Message}. Falling back to FFmpegScreenSource.");
            }
        }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                var raw = new WindowsRawScreenVideoSource(captureArea, captureFps);
                diagnosticLog.LogInfo($"Teacher demo capture source created for {studentBaseUrl}: WindowsRawScreenVideoSource raw=BGRA.");
                return raw;
            }
            catch (Exception ex)
            {
                diagnosticLog.LogWarning($"Teacher demo Windows raw capture backend failed for {studentBaseUrl}: {ex.Message}. Falling back to FFmpegScreenSource.");
            }
        }

        try
        {
            var source = new FFmpegScreenSource(GetScreenInputPath(), captureArea, captureFps);
            diagnosticLog.LogInfo($"Teacher demo capture source created for {studentBaseUrl}: FFmpegScreenSource input={GetScreenInputPath()}.");
            return source;
        }
        catch
        {
            // Keep demo flow operational while we transition away from FFmpeg-based screen capture.
            var fallback = new VideoTestPatternSource(new FFmpegVideoEncoder());
            fallback.SetFrameRate(captureFps);
            diagnosticLog.LogWarning($"Teacher demo capture source fallback activated for {studentBaseUrl}: VideoTestPatternSource.");
            return fallback;
        }
    }

    private static string GetScreenInputPath()
    {
        if (OperatingSystem.IsWindows())
        {
            return "desktop";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "1";
        }

        if (OperatingSystem.IsLinux())
        {
            return ":0.0";
        }

        return "desktop";
    }
}
