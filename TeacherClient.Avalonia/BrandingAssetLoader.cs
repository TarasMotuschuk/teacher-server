using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TeacherClient.CrossPlatform;

internal static class BrandingAssetLoader
{
    private const string ResourcePrefix = "avares://TeacherClient.Avalonia/Assets/Branding/";
    private static readonly Dictionary<string, Bitmap?> BitmapCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, WindowIcon?> IconCache = new(StringComparer.OrdinalIgnoreCase);

    public static WindowIcon? LoadWindowIcon(string relativePath)
    {
        if (IconCache.TryGetValue(relativePath, out var cached))
        {
            return cached;
        }

        try
        {
            using var stream = AssetLoader.Open(new Uri(ResourcePrefix + relativePath));
            var icon = new WindowIcon(stream);
            IconCache[relativePath] = icon;
            return icon;
        }
        catch
        {
            IconCache[relativePath] = null;
            return null;
        }
    }

    public static Bitmap? LoadBitmap(string relativePath)
    {
        if (BitmapCache.TryGetValue(relativePath, out var cached))
        {
            return cached;
        }

        try
        {
            using var stream = AssetLoader.Open(new Uri(ResourcePrefix + relativePath));
            var bitmap = new Bitmap(stream);
            BitmapCache[relativePath] = bitmap;
            return bitmap;
        }
        catch
        {
            BitmapCache[relativePath] = null;
            return null;
        }
    }
}
