using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using TeacherClient.CrossPlatform.Models;

namespace TeacherClient.CrossPlatform.Services;

public static class AppUiThemeApplier
{
    public static void Apply(AppUiTheme theme)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        app.RequestedThemeVariant = theme == AppUiTheme.Light ? ThemeVariant.Light : ThemeVariant.Dark;

        foreach (var (key, color) in GetPalette(theme))
        {
            app.Resources[key] = new SolidColorBrush(color);
        }
    }

    public static IBrush GetBrushOrFallback(string key, Color fallback)
    {
        var app = Application.Current;
        if (app is null)
        {
            return new SolidColorBrush(fallback);
        }

        var found = app.TryGetResource(key, app.ActualThemeVariant, out var res);
        if (found && res is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallback);
    }

    private static IEnumerable<KeyValuePair<string, Color>> GetPalette(AppUiTheme theme)
    {
        if (theme == AppUiTheme.Light)
        {
            return LightPalette();
        }

        return DarkPalette();
    }

    private static IEnumerable<KeyValuePair<string, Color>> DarkPalette()
    {
        foreach (var pair in SharedAccentBrushes())
        {
            yield return pair;
        }

        yield return Key("AppWindowBackgroundBrush", Color.Parse("#2B2B2B"));
        yield return Key("AppChromeForegroundBrush", Color.Parse("#F8FAFC"));
        yield return Key("AppStatusBarBackgroundBrush", Color.Parse("#17202A"));
        yield return Key("SplashWindowBackgroundBrush", Color.Parse("#0B1220"));
        yield return Key("TabActionBackgroundBrush", Color.Parse("#4A4A4A"));
        yield return Key("TabActionForegroundBrush", Color.Parse("#F8FAFC"));
        yield return Key("TabActionBorderBrush", Color.Parse("#686868"));
        yield return Key("TabActionPointerOverBackgroundBrush", Color.Parse("#5A5A5A"));
        yield return Key("TabActionPointerOverBorderBrush", Color.Parse("#848484"));
        yield return Key("TabActionPressedBackgroundBrush", Color.Parse("#414141"));
        yield return Key("TabActionPressedBorderBrush", Color.Parse("#979797"));
        yield return Key("FilePanelBackgroundBrush", Color.Parse("#0F172A"));
        yield return Key("FilePanelBorderInactiveBrush", Color.Parse("#334155"));
        yield return Key("DriveSpaceForegroundBrush", Color.Parse("#A7ADB4"));
        yield return Key("RemoteManagementHintForegroundBrush", Color.Parse("#94A3B8"));
        yield return Key("RemoteTileOuterBackgroundBrush", Color.Parse("#BFC7D0"));
        yield return Key("RemoteTileInnerBackgroundBrush", Color.Parse("#171D24"));
        yield return Key("RemoteTilePreviewBackgroundBrush", Color.Parse("#1A1F26"));
        yield return Key("RemoteTileTitleForegroundBrush", Color.Parse("#FFFFFF"));
        yield return Key("RemoteTileStatusForegroundBrush", Color.Parse("#CBD5E1"));
        yield return Key("FooterBackgroundBrush", Color.Parse("#36404A"));
        yield return Key("FooterForegroundBrush", Color.Parse("#D4D9DF"));
        yield return Key("RemoteTileBorderDefaultBrush", Color.Parse("#94A3B8"));
        yield return Key("RemoteTileBorderHoverBrush", Color.Parse("#60A5FA"));
        yield return Key("RemoteTileBorderSelectedBrush", Color.Parse("#2563EB"));
        yield return Key("RemoteTileBorderSelectedHoverBrush", Color.Parse("#3B82F6"));
    }

    private static IEnumerable<KeyValuePair<string, Color>> LightPalette()
    {
        foreach (var pair in SharedAccentBrushes())
        {
            yield return pair;
        }

        yield return Key("AppWindowBackgroundBrush", Color.Parse("#F1F5F9"));
        yield return Key("AppChromeForegroundBrush", Color.Parse("#0F172A"));
        yield return Key("AppStatusBarBackgroundBrush", Color.Parse("#FFFFFF"));
        yield return Key("SplashWindowBackgroundBrush", Color.Parse("#E8EEF4"));
        yield return Key("TabActionBackgroundBrush", Color.Parse("#E2E8F0"));
        yield return Key("TabActionForegroundBrush", Color.Parse("#0F172A"));
        yield return Key("TabActionBorderBrush", Color.Parse("#CBD5E1"));
        yield return Key("TabActionPointerOverBackgroundBrush", Color.Parse("#CBD5E1"));
        yield return Key("TabActionPointerOverBorderBrush", Color.Parse("#94A3B8"));
        yield return Key("TabActionPressedBackgroundBrush", Color.Parse("#94A3B8"));
        yield return Key("TabActionPressedBorderBrush", Color.Parse("#64748B"));
        yield return Key("FilePanelBackgroundBrush", Color.Parse("#FFFFFF"));
        yield return Key("FilePanelBorderInactiveBrush", Color.Parse("#CBD5E1"));
        yield return Key("DriveSpaceForegroundBrush", Color.Parse("#64748B"));
        yield return Key("RemoteManagementHintForegroundBrush", Color.Parse("#64748B"));
        yield return Key("RemoteTileOuterBackgroundBrush", Color.Parse("#E2E8F0"));
        yield return Key("RemoteTileInnerBackgroundBrush", Color.Parse("#FFFFFF"));
        yield return Key("RemoteTilePreviewBackgroundBrush", Color.Parse("#F1F5F9"));
        yield return Key("RemoteTileTitleForegroundBrush", Color.Parse("#0F172A"));
        yield return Key("RemoteTileStatusForegroundBrush", Color.Parse("#475569"));
        yield return Key("FooterBackgroundBrush", Color.Parse("#E2E8F0"));
        yield return Key("FooterForegroundBrush", Color.Parse("#334155"));
        yield return Key("RemoteTileBorderDefaultBrush", Color.Parse("#94A3B8"));
        yield return Key("RemoteTileBorderHoverBrush", Color.Parse("#3B82F6"));
        yield return Key("RemoteTileBorderSelectedBrush", Color.Parse("#2563EB"));
        yield return Key("RemoteTileBorderSelectedHoverBrush", Color.Parse("#1D4ED8"));
    }

    private static IEnumerable<KeyValuePair<string, Color>> SharedAccentBrushes()
    {
        yield return Key("FilePanelBorderActiveBrush", Color.Parse("#2563EB"));
        yield return Key("ListBoxItemSelectedAccentBrush", Color.Parse("#3B82F6"));
    }

    private static KeyValuePair<string, Color> Key(string key, Color color)
        => new(key, color);
}
