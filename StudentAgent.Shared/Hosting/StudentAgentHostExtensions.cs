using Microsoft.AspNetCore.Mvc;
using StudentAgent.Auth;
using StudentAgent.Services;
using StudentAgent.UI.Localization;
using Teacher.Common.Contracts;

namespace StudentAgent.Hosting;

public static class StudentAgentHostExtensions
{
    public static void AddStudentAgentRuntimeServices(this IServiceCollection services, IConfiguration configuration, bool includeBackgroundPolicies = false)
    {
        services.Configure<AgentOptions>(configuration.GetSection(AgentOptions.SectionName));
        services.AddSingleton<AgentSettingsStore>();
        services.AddSingleton<AgentLogService>();
        services.AddSingleton<ProcessService>();
        services.AddSingleton<FileService>();
        services.AddSingleton<ServerInfoService>();
        services.AddSingleton<NetworkIdentityService>();
        services.AddHostedService<AgentDiscoveryService>();

        if (includeBackgroundPolicies)
        {
            services.AddHostedService<BrowserLockEnforcementService>();
        }

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
    }

    public static void ConfigureStudentAgentWeb(this WebApplication app)
    {
        var settingsStore = app.Services.GetRequiredService<AgentSettingsStore>();
        StudentAgentText.SetLanguage(settingsStore.Current.Language);

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

        app.MapGet("/api/info", (ServerInfoService service) => Results.Ok(service.GetInfo()));
        app.MapGet("/api/processes", (ProcessService service) => Results.Ok(service.GetProcesses()));
        app.MapGet("/api/processes/{processId:int}", (int processId, ProcessService service) =>
        {
            try
            {
                return Results.Ok(service.GetProcessDetails(processId));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
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

        app.MapPost("/api/processes/restart", ([FromBody] RestartProcessRequest request, [FromServices] ProcessService service) =>
        {
            try
            {
                return Results.Ok(service.RestartProcess(request.ProcessId));
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

        app.MapPost("/api/input-lock", ([FromBody] InputLockStateRequest request, [FromServices] AgentSettingsStore store, [FromServices] AgentLogService agentLog) =>
        {
            try
            {
                store.UpdateInputLock(request.Enabled);
                agentLog.LogInfo(request.Enabled ? StudentAgentText.InputLockEnabledLog : StudentAgentText.InputLockDisabledLog);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/power", ([FromBody] PowerActionRequest request, [FromServices] ProcessService service, [FromServices] AgentLogService agentLog) =>
        {
            try
            {
                var logMessage = request.Action switch
                {
                    PowerActionKind.Shutdown => StudentAgentText.ShutdownRequestedLog,
                    PowerActionKind.Restart => StudentAgentText.RestartRequestedLog,
                    PowerActionKind.LogOff => StudentAgentText.LogOffRequestedLog,
                    _ => throw new ArgumentOutOfRangeException(nameof(request.Action), request.Action, "Unsupported power action.")
                };

                agentLog.LogWarning(logMessage);
                service.ExecutePowerAction(request.Action);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapGet("/api/files/roots", ([FromServices] FileService service) => Results.Ok(service.GetRoots()));

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
    }
}
