namespace TeacherClient.CrossPlatform.Services;

public enum DemoCaptureTargetKind
{
    Screen = 0,
    Window = 1,
}

public sealed record DemoCaptureTarget(
    DemoCaptureTargetKind Kind,
    int CaptureX,
    int CaptureY,
    int CaptureWidth,
    int CaptureHeight,
    long? PlatformWindowId = null,
    string? WindowTitle = null);

