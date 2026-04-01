namespace StudentAgent.Services;

internal static class StudentAgentPathHelper
{
    public static string GetRootDirectory()
    {
        var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(commonAppData))
        {
            var root = Path.Combine(commonAppData, "TeacherServer", "StudentAgent");
            EnsureDirectoryExists(root);
            return root;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            var root = Path.Combine(localAppData, "TeacherServer", "StudentAgent");
            EnsureDirectoryExists(root);
            return root;
        }

        var fallback = Path.Combine(AppContext.BaseDirectory, "data");
        EnsureDirectoryExists(fallback);
        return fallback;
    }

    public static string GetLogsDirectory()
        => Path.Combine(GetRootDirectory(), "logs");

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}
