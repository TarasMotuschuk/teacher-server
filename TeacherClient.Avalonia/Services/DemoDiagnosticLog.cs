namespace TeacherClient.CrossPlatform.Services;

public sealed class DemoDiagnosticLog
{
    private readonly object _sync = new();
    private readonly string _logDirectory;
    private readonly string _logFilePath;

    public DemoDiagnosticLog(string logFilePath)
    {
        _logFilePath = logFilePath;
        _logDirectory = Path.GetDirectoryName(logFilePath) ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(_logDirectory);
    }

    public string LogFilePath => _logFilePath;

    public void LogInfo(string message) => Write("INFO", message);

    public void LogWarning(string message) => Write("WARN", message);

    public void LogError(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
        lock (_sync)
        {
            try
            {
                Directory.CreateDirectory(_logDirectory);
                File.AppendAllText(_logFilePath, line);
            }
            catch
            {
                // Demo diagnostics must never break the app.
            }
        }
    }
}
