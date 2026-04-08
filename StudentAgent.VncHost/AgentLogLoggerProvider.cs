using Microsoft.Extensions.Logging;
using StudentAgent.Services;

namespace StudentAgent.VncHost;

internal sealed class AgentLogLoggerProvider : ILoggerProvider
{
    private readonly AgentLogService _logService;

    public AgentLogLoggerProvider(AgentLogService logService)
    {
        _logService = logService;
    }

    public ILogger CreateLogger(string categoryName) => new AgentLogLogger(_logService, categoryName);

    public void Dispose()
    {
    }
}
