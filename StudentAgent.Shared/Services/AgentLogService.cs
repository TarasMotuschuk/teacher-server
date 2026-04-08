namespace StudentAgent.Services;

public sealed class AgentLogService
{
    private readonly object _sync = new();
    private readonly string _logDirectory;
    private readonly string _logFilePath;

    public AgentLogService()
    {
        _logDirectory = GetLogDirectory();
        _logFilePath = Path.Combine(_logDirectory, "studentagent.log");
        Directory.CreateDirectory(_logDirectory);
    }

    public string LogFilePath => _logFilePath;

    public void LogInfo(string message) => Write("INFO", message);

    public void LogWarning(string message) => Write("WARN", message);

    public void LogError(string message) => Write("ERROR", message);

    public string ReadAll()
    {
        lock (_sync)
        {
            return File.Exists(_logFilePath) ? File.ReadAllText(_logFilePath) : string.Empty;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            Directory.CreateDirectory(_logDirectory);
            File.WriteAllText(_logFilePath, string.Empty);
        }
    }

    private static string GetLogDirectory()
        => StudentAgentPathHelper.GetLogsDirectory();

    private void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
        lock (_sync)
        {
            try
            {
                Directory.CreateDirectory(_logDirectory);
                File.AppendAllText(_logFilePath, line);
            }
            catch
            {
                // Logging must never break the agent or API responses.
            }
        }
    }
}
