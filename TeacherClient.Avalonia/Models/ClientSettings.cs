using Teacher.Common.Localization;

namespace TeacherClient.CrossPlatform.Models;

public sealed record ClientSettings(
    string SharedSecret,
    UiLanguage Language,
    string BulkCopyDestinationPath,
    string StudentWorkRootPath,
    string StudentWorkFolderName,
    int DesktopIconAutoRestoreMinutes,
    int BrowserLockCheckIntervalSeconds,
    AppUiTheme Theme,
    string TestPlatformBaseUrl)
{
    public static ClientSettings Default { get; } = new(
        "change-this-secret",
        UiLanguageExtensions.GetDefault(),
        @"C:\TeacherDrops",
        @"C:\Users\Public\Documents",
        "StudentWorks",
        30,
        60,
        AppUiTheme.Dark,
        "http://127.0.0.1:5000");
}
