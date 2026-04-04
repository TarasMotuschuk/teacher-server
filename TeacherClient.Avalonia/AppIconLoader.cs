using Avalonia.Controls;

namespace TeacherClient.CrossPlatform;

internal static class AppIconLoader
{
    public static WindowIcon? Load() => BrandingAssetLoader.LoadWindowIcon("ClassCommander-icon-cropped.png");
}
