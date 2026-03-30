using StudentAgent.Hosting;
using StudentAgent.Services;
using StudentAgent.Service.Services;
using StudentAgent.UI.Localization;

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseWindowsService(options =>
    {
        options.ServiceName = "StudentAgent Service";
    });

    builder.Services.AddStudentAgentRuntimeServices(builder.Configuration, includeBackgroundPolicies: true);
    builder.Services.AddHostedService<UiHostLauncherService>();

    var app = builder.Build();
    var settingsStore = app.Services.GetRequiredService<AgentSettingsStore>();
    var logService = app.Services.GetRequiredService<AgentLogService>();
    StudentAgentText.SetLanguage(settingsStore.Current.Language);

    app.ConfigureStudentAgentWeb();
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        logService.LogInfo($"StudentAgent.Service started on port {settingsStore.Current.Port}.");
    });
    app.Lifetime.ApplicationStopping.Register(() =>
    {
        logService.LogInfo("StudentAgent.Service stopping.");
    });

    logService.LogInfo("StudentAgent.Service starting.");
    app.Run();
}
catch (Exception ex)
{
    var startupLogPath = GetStartupErrorLogPath();
    Directory.CreateDirectory(Path.GetDirectoryName(startupLogPath)!);
    File.WriteAllText(startupLogPath, ex.ToString());

    if (Environment.UserInteractive)
    {
        Console.Error.WriteLine(ex);
    }
}

static string GetStartupErrorLogPath()
{
    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    var root = string.IsNullOrWhiteSpace(localAppData)
        ? Path.Combine(AppContext.BaseDirectory, "logs")
        : Path.Combine(localAppData, "TeacherServer", "StudentAgent", "logs");

    return Path.Combine(root, "studentagent-service-startup-error.log");
}
