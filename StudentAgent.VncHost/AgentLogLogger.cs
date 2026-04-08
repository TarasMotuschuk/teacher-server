using Microsoft.Extensions.Logging;
using StudentAgent.Services;

namespace StudentAgent.VncHost;

internal sealed class AgentLogLogger : ILogger
{
    private readonly AgentLogService _logService;
    private readonly string _categoryName;

    public AgentLogLogger(AgentLogService logService, string categoryName)
    {
        _logService = logService;
        _categoryName = categoryName;
    }

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
        => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        if (string.IsNullOrWhiteSpace(message) && exception is null)
        {
            return;
        }

        var fullMessage = $"[{_categoryName}] {message}";
        if (exception is not null)
        {
            fullMessage = $"{fullMessage}{Environment.NewLine}{exception}";
        }

        switch (logLevel)
        {
            case LogLevel.Warning:
                _logService.LogWarning(fullMessage);
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                _logService.LogError(fullMessage);
                break;
            default:
                _logService.LogInfo(fullMessage);
                break;
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
