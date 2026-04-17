using System.Drawing;
using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Encoders;

namespace TeacherClient.CrossPlatform.Services;

public sealed class DemoVideoSourceFactory
{
    public IVideoSource CreateSource(Rectangle captureArea, int captureFps, DemoDiagnosticLog diagnosticLog, string studentBaseUrl)
    {
        if (OperatingSystem.IsMacOS())
        {
            if (!MacOsScreenCaptureProducer.HasScreenCaptureAccess(requestIfMissing: true))
            {
                var message =
                    "macOS Screen Recording permission is not granted for ClassCommander. " +
                    "Open System Settings -> Privacy & Security -> Screen Recording, enable ClassCommander, then relaunch the app.";
                diagnosticLog.LogError($"Teacher demo macOS screen recording permission missing for {studentBaseUrl}: {message}");
                throw new InvalidOperationException(message);
            }

            try
            {
                var raw = new MacOsRawScreenVideoSource(captureArea, captureFps);
                diagnosticLog.LogInfo($"Teacher demo capture source created for {studentBaseUrl}: MacOsRawScreenVideoSource raw=BGRA.");
                return raw;
            }
            catch (Exception ex)
            {
                diagnosticLog.LogWarning($"Teacher demo macOS raw capture backend failed for {studentBaseUrl}: {ex.Message}. Falling back to test pattern.");
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
                diagnosticLog.LogWarning($"Teacher demo Windows raw capture backend failed for {studentBaseUrl}: {ex.Message}. Falling back to test pattern.");
            }
        }

        try
        {
            var fallback = new VideoTestPatternSource(new VpxVideoEncoder());
            fallback.SetFrameRate(captureFps);
            diagnosticLog.LogWarning($"Teacher demo capture source fallback for {studentBaseUrl}: VideoTestPatternSource (VP8/libvpx).");
            return fallback;
        }
        catch (Exception ex)
        {
            diagnosticLog.LogError($"Teacher demo could not create any video source for {studentBaseUrl}: {ex.Message}");
            throw;
        }
    }
}
