using StudentAgent.Hosting;
using StudentAgent.Services;
using StudentAgent.Service.Services;
using StudentAgent.UI.Localization;
using Microsoft.AspNetCore.Mvc;
using Teacher.Common.Contracts;

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
    builder.Services.AddSingleton<DesktopIconLayoutService>();
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
    app.MapGet("/api/desktop-icons/layouts", ([FromServices] DesktopIconLayoutService service) =>
    {
        return Results.Ok(service.GetLayouts());
    });
    app.MapGet("/api/desktop-icons/layout", (string? name, [FromServices] DesktopIconLayoutService service) =>
    {
        try
        {
            return Results.Ok(service.GetLayout(name));
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });
    app.MapPost("/api/desktop-icons/save", ([FromBody] SaveDesktopIconLayoutRequest request, [FromServices] DesktopIconLayoutService service) =>
    {
        try
        {
            return Results.Ok(service.SaveLayout(request.LayoutName));
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });
    app.MapPost("/api/desktop-icons/restore", ([FromBody] RestoreDesktopIconLayoutRequest request, [FromServices] DesktopIconLayoutService service) =>
    {
        try
        {
            return Results.Ok(service.RestoreLayout(request.LayoutName));
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });
    app.MapPost("/api/desktop-icons/apply", ([FromBody] ApplyDesktopIconLayoutRequest request, [FromServices] DesktopIconLayoutService service) =>
    {
        try
        {
            return Results.Ok(service.ApplyLayout(request));
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
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
    => Path.Combine(StudentAgentPathHelper.GetLogsDirectory(), "studentagent-service-startup-error.log");
