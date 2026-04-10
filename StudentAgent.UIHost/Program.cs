using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudentAgent;
using StudentAgent.Services;
using StudentAgent.UI.Localization;
using StudentAgent.UIHost;
using StudentAgent.UIHost.DesktopIcons;

try
{
    ApplicationConfiguration.Initialize();
    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .Build();

    var services = new ServiceCollection();
    services.Configure<AgentOptions>(configuration.GetSection(AgentOptions.SectionName));
    services.AddSingleton<AgentLogService>();
    services.AddSingleton<AgentSettingsStore>();
    services.AddSingleton<ProcessService>();

    using var serviceProvider = services.BuildServiceProvider();
    var settingsStore = serviceProvider.GetRequiredService<AgentSettingsStore>();
    var logService = serviceProvider.GetRequiredService<AgentLogService>();
    var processService = serviceProvider.GetRequiredService<ProcessService>();

    StudentAgentText.SetLanguage(settingsStore.Current.Language);

    if (DesktopIconLayoutCommandRunner.TryExecute(args, logService, out var commandResult, out _))
    {
        Environment.ExitCode = commandResult.Succeeded ? 0 : 1;
        return;
    }

    Application.ThreadException += (_, exceptionArgs) =>
    {
        logService.LogError($"UIHost thread exception: {exceptionArgs.Exception}");
        MessageBox.Show(
            exceptionArgs.Exception.ToString(),
            StudentAgentText.StudentAgentUiError,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    };

    AppDomain.CurrentDomain.UnhandledException += (_, exceptionArgs) =>
    {
        if (exceptionArgs.ExceptionObject is Exception exception)
        {
            logService.LogError($"UIHost unhandled exception: {exception}");
            MessageBox.Show(
                exception.ToString(),
                StudentAgentText.StudentAgentFatalError,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    };

    logService.LogInfo("StudentAgent.UIHost starting.");
    using var context = new UIHostApplicationContext(settingsStore, logService, processService);
    Application.Run(context);
}
catch (Exception ex)
{
    var startupLogPath = GetStartupErrorLogPath();
    Directory.CreateDirectory(Path.GetDirectoryName(startupLogPath)!);
    File.WriteAllText(startupLogPath, ex.ToString());
}

static string GetStartupErrorLogPath()
    => Path.Combine(StudentAgentPathHelper.GetLogsDirectory(), "studentagent-uihost-startup-error.log");
