using System.Globalization;

namespace Teacher.Common.Localization;

public enum UiLanguage
{
    English,
    Ukrainian,
}

public static class UiLanguageExtensions
{
    public static UiLanguage Normalize(this UiLanguage value)
        => ((UiLanguage?)value).Normalize();

    public static UiLanguage GetDefault()
    {
        return string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "uk", StringComparison.OrdinalIgnoreCase)
            ? UiLanguage.Ukrainian
            : UiLanguage.English;
    }

    public static UiLanguage Normalize(this UiLanguage? value)
        => value ?? GetDefault();

    public static string ToCode(this UiLanguage value)
        => value == UiLanguage.Ukrainian ? "uk" : "en";

    public static UiLanguage Parse(string? value)
    {
        return string.Equals(value, "uk", StringComparison.OrdinalIgnoreCase)
            ? UiLanguage.Ukrainian
            : UiLanguage.English;
    }
}
