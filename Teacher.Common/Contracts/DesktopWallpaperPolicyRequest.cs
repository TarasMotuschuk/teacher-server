namespace Teacher.Common.Contracts;

/// <param name="WallpaperPath">Full local path on the student PC (e.g. under <c>C:\Windows\Web\Wallpaper</c>).</param>
/// <param name="WallpaperStyle">0..5 — same as Group Policy Desktop Wallpaper (Center, Tile, Stretch, Fit, Fill, Span).</param>
public sealed record DesktopWallpaperPolicyRequest(
    string WallpaperPath,
    int WallpaperStyle);
