namespace StudentAgent.Services;

internal static class StudentAgentPathHelper
{
    public static string GetRootDirectory()
    {
        var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(commonAppData))
        {
            return Path.Combine(commonAppData, "TeacherServer", "StudentAgent");
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return Path.Combine(localAppData, "TeacherServer", "StudentAgent");
        }

        return Path.Combine(AppContext.BaseDirectory, "data");
    }

    public static string GetLogsDirectory()
        => Path.Combine(GetRootDirectory(), "logs");
}
