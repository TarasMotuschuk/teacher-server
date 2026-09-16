using System.Drawing;
using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Encoders;

namespace TeacherClient.CrossPlatform.Services;

public sealed class DemoVideoSourceFactory
{
    public IVideoSource CreateSource(DemoCaptureTarget target, int captureFps, DemoDiagnosticLog diagnosticLog, string studentBaseUrl)
    {
        if (OperatingSystem.IsMacOS())
        {
            if (!MacOsScreenCaptureProducer.HasScreenCaptureAccess())
            {
                var message =
                    "macOS Screen Recording permission is not granted for ClassCommander. " +
                    "Open System Settings -> Privacy & Security -> Screen Recording, enable ClassCommander, then relaunch the app.";
                diagnosticLog.LogError($"Teacher demo macOS screen recording permission missing for {studentBaseUrl}: {message}");
                throw new InvalidOperationException(message);
            }

            try
            {
                if (target.Kind == DemoCaptureTargetKind.Window && target.PlatformWindowId is long windowId)
                {
                    var raw = new MacOsRawWindowVideoSource(windowId, target.WindowTitle, captureFps);
                    diagnosticLog.LogInfo($"Teacher demo capture source created for {studentBaseUrl}: MacOsRawWindowVideoSource windowId={windowId}.");
                    return raw;
                }
                else
                {
                    var captureArea = new Rectangle(target.CaptureX, target.CaptureY, target.CaptureWidth, target.CaptureHeight);
                    var raw = new MacOsRawScreenVideoSource(captureArea, captureFps);
                    diagnosticLog.LogInfo($"Teacher demo capture source created for {studentBaseUrl}: MacOsRawScreenVideoSource raw=BGRA.");
                    return raw;
                }
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
                if (target.Kind == DemoCaptureTargetKind.Window && target.PlatformWindowId is long hwnd)
                {
                    var raw = new WindowsRawWindowVideoSource((nint)hwnd, target.WindowTitle, captureFps);
                    diagnosticLog.LogInfo($"Teacher demo capture source created for {studentBaseUrl}: WindowsRawWindowVideoSource hwnd=0x{hwnd:X}.");
                    return raw;
                }
                else
                {
                    var captureArea = new Rectangle(target.CaptureX, target.CaptureY, target.CaptureWidth, target.CaptureHeight);
                    var raw = new WindowsRawScreenVideoSource(captureArea, captureFps);
                    diagnosticLog.LogInfo($"Teacher demo capture source created for {studentBaseUrl}: WindowsRawScreenVideoSource raw=BGRA.");
                    return raw;
                }
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
