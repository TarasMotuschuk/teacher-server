using System.Text.Json;
using System.Text.Json.Serialization;

namespace Teacher.Common.Localization;

/// <summary>
/// Shared ClassCommander UI preferences (language) used by teacher client, Test Editor, and Test Runner.
/// </summary>
public static class ClassCommanderUiSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static readonly object Sync = new();

    public static UiLanguage LoadLanguage()
    {
        lock (Sync)
        {
            try
            {
                var path = GetSettingsPath();
                if (!File.Exists(path))
                {
                    return UiLanguageExtensions.GetDefault();
                }

                var dto = JsonSerializer.Deserialize<UiSettingsDto>(File.ReadAllText(path), JsonOptions);
                return dto?.Language.Normalize() ?? UiLanguageExtensions.GetDefault();
            }
            catch
            {
                return UiLanguageExtensions.GetDefault();
            }
        }
    }

    public static void SaveLanguage(UiLanguage language)
    {
        lock (Sync)
        {
            var path = GetSettingsPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var dto = new UiSettingsDto(language.Normalize());
            File.WriteAllText(path, JsonSerializer.Serialize(dto, JsonOptions));
        }
    }

    private static string GetSettingsPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(AppContext.BaseDirectory, "data");
        }

        return Path.Combine(root, "ClassCommander", "ui-settings.json");
    }

    private sealed record UiSettingsDto(UiLanguage Language);
}
