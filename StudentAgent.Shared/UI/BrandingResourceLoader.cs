using System.Reflection;

namespace StudentAgent.UI;

internal static class BrandingResourceLoader
{
    private const string ResourcePrefix = "StudentAgent.UIHost.Assets.Branding.";
    private static readonly Dictionary<string, Bitmap?> BitmapCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Icon?> IconCache = new(StringComparer.OrdinalIgnoreCase);

    public static Icon? LoadIcon(string relativePath)
    {
        if (IconCache.TryGetValue(relativePath, out var cached))
        {
            return cached;
        }

        using var stream = OpenResource(relativePath);
        if (stream is null)
        {
            IconCache[relativePath] = null;
            return null;
        }

        var icon = new Icon(stream);
        IconCache[relativePath] = icon;
        return icon;
    }

    // Cached master bitmap is cloned per call so multiple forms/controls never share one GDI+ Image (WinForms paint is not thread-safe on a single instance).
    public static Bitmap? LoadBitmap(string relativePath)
    {
        if (!BitmapCache.TryGetValue(relativePath, out var cached))
        {
            using var stream = OpenResource(relativePath);
            if (stream is null)
            {
                BitmapCache[relativePath] = null;
                return null;
            }

            using var image = Image.FromStream(stream);
            cached = new Bitmap(image);
            BitmapCache[relativePath] = cached;
        }

        return cached is null ? null : (Bitmap)cached.Clone();
    }

    private static Stream? OpenResource(string relativePath)
    {
        var resourceName = ResourcePrefix + relativePath
            .Replace('\\', '.')
            .Replace('/', '.');

        return Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
    }
}
