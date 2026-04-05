namespace Teacher.Common.Vnc;

/// <summary>
/// Caps CPU work when downscaling very large remote desktops before display.
/// Up to 4K keeps typical classroom/Office resolutions sharp without scaling.
/// </summary>
public static class VncViewerDisplayLimits
{
    public const int MaxFrameWidth = 3840;
    public const int MaxFrameHeight = 2160;
}
