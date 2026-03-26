#nullable enable

using Teacher.Common.Localization;

namespace TeacherClient.Models;

public sealed record ClientSettings(string SharedSecret, UiLanguage Language)
{
    public static ClientSettings Default { get; } = new("change-this-secret", UiLanguageExtensions.GetDefault());
}
