#nullable enable

using Teacher.Common.Localization;

namespace TeacherClient.Models;

public sealed record ClientSettings(
    string SharedSecret,
    UiLanguage Language,
    string BulkCopyDestinationPath,
    string StudentWorkRootPath,
    string StudentWorkFolderName,
    int DesktopIconAutoRestoreMinutes,
    int BrowserLockCheckIntervalSeconds)
{
    public static ClientSettings Default { get; } = new(
        "change-this-secret",
        UiLanguageExtensions.GetDefault(),
        @"C:\TeacherDrops",
        @"C:\Users\Public\Documents",
        "StudentWorks",
        30,
        60);
}
