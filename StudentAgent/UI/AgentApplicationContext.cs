using StudentAgent.Services;
using StudentAgent.UI.Localization;

namespace StudentAgent.UI;

public sealed class AgentApplicationContext : AgentUiApplicationContextBase
{
    private readonly WebApplication _app;
    private readonly AgentLogService _logService;

    public AgentApplicationContext(WebApplication app, AgentSettingsStore settingsStore, AgentLogService logService, ProcessService processService)
        : base(settingsStore, logService, processService)
    {
        _app = app;
        _logService = logService;
    }

    protected override void HandleExitRequested()
    {
        if (!PromptForPassword())
        {
            return;
        }

        if (!IsAdministrator())
        {
            _logService.LogWarning(StudentAgentText.ExitDeniedBecauseNotAdminLog);
            MessageBox.Show(
                StudentAgentText.OnlyAdminCanClose,
                StudentAgentText.ExitDenied,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        ExitThread();
    }

    protected override void OnBeforeExitThreadCore()
    {
        _logService.LogInfo("StudentAgent stopping.");
        _app.StopAsync().GetAwaiter().GetResult();
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
