using Avalonia.Controls;
using Avalonia.Platform;

namespace TeacherClient.CrossPlatform;

internal static class AppIconLoader
{
    private static WindowIcon? _icon;

    public static WindowIcon? Load()
    {
        if (_icon is not null)
        {
            return _icon;
        }

        using var stream = AssetLoader.Open(new Uri("avares://TeacherClient.Avalonia/Assets/ClassCommander-icon-cropped.png"));
        _icon = new WindowIcon(stream);
        return _icon;
    }
}
