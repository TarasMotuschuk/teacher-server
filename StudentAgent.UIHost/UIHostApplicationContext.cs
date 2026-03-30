using StudentAgent.Services;
using StudentAgent.UI;

namespace StudentAgent.UIHost;

public sealed class UIHostApplicationContext : AgentUiApplicationContextBase
{
    private readonly AgentLogService _logService;

    public UIHostApplicationContext(AgentSettingsStore settingsStore, AgentLogService logService, ProcessService processService)
        : base(settingsStore, logService, processService)
    {
        _logService = logService;
    }

    protected override void HandleExitRequested()
    {
        ExitThread();
    }

    protected override void OnBeforeExitThreadCore()
    {
        _logService.LogInfo("StudentAgent.UIHost stopping.");
    }
}
