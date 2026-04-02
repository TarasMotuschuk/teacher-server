using System.Reflection;

namespace TeacherClient;

internal static class AppIconLoader
{
    private static Icon? _icon;

    public static Icon? Load()
    {
        if (_icon is not null)
        {
            return _icon;
        }

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("TeacherClient.Assets.ClassCommander-icon.ico");
        if (stream is null)
        {
            return null;
        }

        _icon = new Icon(stream);
        return _icon;
    }
}
