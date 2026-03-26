using Teacher.Common.Localization;

namespace TeacherClient.CrossPlatform.Models;

public sealed record ClientSettings(string SharedSecret, UiLanguage Language, string BulkCopyDestinationPath)
{
    public static ClientSettings Default { get; } = new("change-this-secret", UiLanguageExtensions.GetDefault(), @"C:\TeacherDrops");
}
