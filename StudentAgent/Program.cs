using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StudentAgent;
using StudentAgent.Auth;
using StudentAgent.Services;
using StudentAgent.UI;
using StudentAgent.UI.Localization;
using Teacher.Common.Contracts;

try
{
    ApplicationConfiguration.Initialize();
    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

    var builder = WebApplication.CreateBuilder(args);

    builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
    builder.Services.AddSingleton<AgentSettingsStore>();
    builder.Services.AddSingleton<AgentLogService>();
    builder.Services.AddSingleton<ProcessService>();
    builder.Services.AddSingleton<FileService>();
    builder.Services.AddSingleton<ServerInfoService>();
    builder.Services.AddSingleton<NetworkIdentityService>();
    builder.Services.AddHostedService<AgentDiscoveryService>();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();
    var settingsStore = app.Services.GetRequiredService<AgentSettingsStore>();
    var logService = app.Services.GetRequiredService<AgentLogService>();
    StudentAgentText.SetLanguage(settingsStore.Current.Language);

    Application.ThreadException += (_, exceptionArgs) =>
    {
        logService.LogError($"UI thread exception: {exceptionArgs.Exception}");
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
            logService.LogError($"Unhandled exception: {exception}");
            MessageBox.Show(
                exception.ToString(),
                StudentAgentText.StudentAgentFatalError,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    };

    app.Urls.Add($"http://0.0.0.0:{settingsStore.Current.Port}");
    app.UseMiddleware<GlobalExceptionLoggingMiddleware>();
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseMiddleware<SharedSecretMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }));

    app.MapGet("/api/info", (ServerInfoService service) =>
    {
        return Results.Ok(service.GetInfo());
    });

    app.MapGet("/api/processes", (ProcessService service) =>
    {
        return Results.Ok(service.GetProcesses());
    });

    app.MapPost("/api/processes/kill", ([FromBody] KillProcessRequest request, [FromServices] ProcessService service) =>
    {
        try
        {
            service.KillProcess(request.ProcessId);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

    app.MapPost("/api/browser-lock", ([FromBody] BrowserLockStateRequest request, [FromServices] AgentSettingsStore store, [FromServices] AgentLogService agentLog) =>
    {
        try
        {
            store.UpdateBrowserLock(request.Enabled);
            agentLog.LogInfo(request.Enabled ? StudentAgentText.BrowserLockEnabledLog : StudentAgentText.BrowserLockDisabledLog);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

    app.MapGet("/api/files/roots", ([FromServices] FileService service) =>
    {
        return Results.Ok(service.GetRoots());
    });

    app.MapGet("/api/files/list", (string? path, [FromServices] FileService service) =>
    {
        try
        {
            return Results.Ok(service.GetDirectory(path ?? string.Empty));
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

    app.MapDelete("/api/files", ([FromBody] DeleteEntryRequest request, [FromServices] FileService service) =>
    {
        try
        {
            service.DeleteEntry(request.FullPath);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

    app.MapPost("/api/files/directories", ([FromBody] CreateDirectoryRequest request, [FromServices] FileService service) =>
    {
        try
        {
            service.CreateDirectory(request.ParentPath, request.Name);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

    app.MapPost("/api/files/clear-directory", ([FromBody] ClearDirectoryRequest request, [FromServices] FileService service) =>
    {
        try
        {
            service.ClearDirectoryContents(request.FullPath);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

    app.MapPost("/api/files/shared-directory", ([FromBody] EnsureSharedDirectoryRequest request, [FromServices] FileService service) =>
    {
        try
        {
            service.EnsureSharedWritableDirectory(request.FullPath);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

    app.MapGet("/api/files/download", (string fullPath, [FromServices] FileService service) =>
    {
        try
        {
            var (fileName, stream, contentType) = service.OpenRead(fullPath);
            return Results.File(stream, contentType, fileName);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

    app.MapPost("/api/files/upload", async (HttpRequest request, [FromServices] FileService service, CancellationToken cancellationToken) =>
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { error = "multipart/form-data is required." });
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files["file"];
        var destinationDirectory = form["destinationDirectory"].ToString();

        if (file is null)
        {
            return Results.BadRequest(new { error = "File is missing." });
        }

        await using var source = file.OpenReadStream();
        await service.SaveFileAsync(destinationDirectory, file.FileName, source, cancellationToken);
        return Results.NoContent();
    });

    logService.LogInfo("StudentAgent starting.");
    using var context = new AgentApplicationContext(app, settingsStore, logService, app.Services.GetRequiredService<ProcessService>());
    app.StartAsync().GetAwaiter().GetResult();
    logService.LogInfo($"StudentAgent started on port {settingsStore.Current.Port}.");
    Application.Run(context);
}
catch (Exception ex)
{
    var startupLogPath = GetStartupErrorLogPath();
    Directory.CreateDirectory(Path.GetDirectoryName(startupLogPath)!);
    File.WriteAllText(startupLogPath, ex.ToString());
    MessageBox.Show(
        $"{StudentAgentText.StartupFailed}{Environment.NewLine}{Environment.NewLine}{ex.Message}{Environment.NewLine}{Environment.NewLine}{StudentAgentText.StartupDetailsWritten}{Environment.NewLine}{startupLogPath}",
        StudentAgentText.StartupError,
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
}

static string GetStartupErrorLogPath()
{
    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    if (string.IsNullOrWhiteSpace(localAppData))
    {
        return Path.Combine(AppContext.BaseDirectory, "studentagent-startup-error.log");
    }

    return Path.Combine(localAppData, "TeacherServer", "StudentAgent", "studentagent-startup-error.log");
}
