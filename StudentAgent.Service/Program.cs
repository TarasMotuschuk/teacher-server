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
    builder.Services.AddSingleton<RemoteShellOpenService>();
    builder.Services.AddSingleton<RemoteCommandService>();
    builder.Services.AddSingleton<PublicDesktopShortcutService>();
    builder.Services.AddHostedService<UiHostLauncherService>();

    var app = builder.Build();
    var settingsStore = app.Services.GetRequiredService<AgentSettingsStore>();
    var logService = app.Services.GetRequiredService<AgentLogService>();
    StudentAgentText.SetLanguage(settingsStore.Current.Language);

    app.ConfigureStudentAgentWeb();
    app.MapPost("/api/files/open", ([FromBody] OpenRemoteEntryRequest request, [FromServices] RemoteShellOpenService service) =>
    {
        try
        {
            service.OpenEntry(request.FullPath);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });
    app.MapPost("/api/commands/run", ([FromBody] RemoteCommandRequest request, [FromServices] RemoteCommandService service) =>
    {
        try
        {
            var executionMode = service.ExecuteScript(request.Script, request.RunAs);
            return Results.Ok(new { mode = executionMode });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });
    app.MapGet("/api/commands/frequent-programs/public-desktop", ([FromServices] PublicDesktopShortcutService service) =>
    {
        return Results.Ok(service.GetPublicDesktopShortcuts());
    });
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
