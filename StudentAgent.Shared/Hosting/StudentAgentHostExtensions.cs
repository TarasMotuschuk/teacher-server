using System.Text;
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
        services.AddSingleton<RegistryService>();
        services.AddHttpClient(nameof(AgentUpdateService));
        services.AddSingleton<AgentUpdateService>();
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

        // Lets session processes (UIHost, VncHost) persist settings via the Windows service when they cannot write HKLM.
        app.MapPost("/api/agent/runtime-settings", ([FromBody] AgentRuntimeSettings snapshot, [FromServices] AgentSettingsStore store) =>
        {
            try
            {
                store.ImportRuntimeSettings(snapshot);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapGet("/api/info", (ServerInfoService service) => Results.Ok(service.GetInfo()));
        app.MapGet("/api/processes", () => Results.Ok(ProcessService.GetProcesses()));
        app.MapGet("/api/processes/{processId:int}", (int processId) =>
        {
            try
            {
                return Results.Ok(ProcessService.GetProcessDetails(processId));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/processes/kill", ([FromBody] KillProcessRequest request) =>
        {
            try
            {
                ProcessService.KillProcess(request.ProcessId);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/processes/restart", ([FromBody] RestartProcessRequest request) =>
        {
            try
            {
                return Results.Ok(ProcessService.RestartProcess(request.ProcessId));
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
                store.UpdateInputLock(request.Enabled, request.VisualMode);
                agentLog.LogInfo(request.Enabled ? StudentAgentText.InputLockEnabledLog : StudentAgentText.InputLockDisabledLog);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/policy-settings", ([FromBody] StudentPolicySettingsRequest request, [FromServices] AgentSettingsStore store, [FromServices] AgentLogService agentLog) =>
        {
            try
            {
                store.UpdatePolicySettings(request.DesktopIconAutoRestoreMinutes, request.BrowserLockCheckIntervalSeconds);
                agentLog.LogInfo($"Teacher updated policy settings: desktop icon auto-restore {Math.Max(1, request.DesktopIconAutoRestoreMinutes)} min, browser-lock check {Math.Max(5, request.BrowserLockCheckIntervalSeconds)} s.");
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/power", ([FromBody] PowerActionRequest request, [FromServices] AgentLogService agentLog) =>
        {
            try
            {
                var logMessage = request.Action switch
                {
                    PowerActionKind.Shutdown => StudentAgentText.ShutdownRequestedLog,
                    PowerActionKind.Restart => StudentAgentText.RestartRequestedLog,
                    PowerActionKind.LogOff => StudentAgentText.LogOffRequestedLog,
                    _ => throw new ArgumentOutOfRangeException(nameof(request), request.Action, "Unsupported power action."),
                };

                agentLog.LogWarning(logMessage);
                ProcessService.ExecutePowerAction(request.Action);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapGet("/api/registry/keys", (string? path, [FromServices] RegistryService service) =>
        {
            try
            {
                return Results.Ok(service.GetSubKeys(path ?? string.Empty));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapGet("/api/registry/values", (string? path, [FromServices] RegistryService service) =>
        {
            try
            {
                return Results.Ok(service.GetValues(path ?? string.Empty));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapGet("/api/registry/values/edit", (string? path, [FromServices] RegistryService service) =>
        {
            try
            {
                return Results.Ok(service.GetValuesForEdit(path ?? string.Empty));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/registry/values", ([FromBody] SetRegistryValueRequest request, [FromServices] RegistryService service) =>
        {
            try
            {
                service.SetValue(request.Path, request.Name, request.Type, request.Data);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapDelete("/api/registry/values", ([FromBody] DeleteRegistryValueRequest request, [FromServices] RegistryService service) =>
        {
            try
            {
                service.DeleteValue(request.Path, request.Name);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/registry/keys", ([FromBody] CreateRegistryKeyRequest request, [FromServices] RegistryService service) =>
        {
            try
            {
                service.CreateSubKey(request.ParentPath, request.KeyName);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapDelete("/api/registry/keys", ([FromBody] DeleteRegistryKeyRequest request, [FromServices] RegistryService service) =>
        {
            try
            {
                service.DeleteSubKey(request.Path);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapGet("/api/registry/export", (string? path, [FromServices] RegistryService service) =>
        {
            try
            {
                var normalizedPath = path ?? string.Empty;
                var content = service.ExportKey(normalizedPath);
                return Results.File(
                    Encoding.Unicode.GetBytes(content),
                    "application/x-ms-regedit",
                    GetRegistryExportFileName(normalizedPath));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/registry/import", async (HttpRequest request, [FromServices] RegistryService service, CancellationToken cancellationToken) =>
        {
            try
            {
                if (!request.HasFormContentType)
                {
                    return Results.BadRequest(new { error = "multipart/form-data is required." });
                }

                var form = await request.ReadFormAsync(cancellationToken);
                var file = form.Files["file"];
                if (file is null || file.Length == 0)
                {
                    return Results.BadRequest(new { error = "Registry file is missing." });
                }

                using var stream = file.OpenReadStream();
                var content = await ReadRegistryTextAsync(stream, cancellationToken);
                return Results.Ok(service.ImportRegFile(content));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapGet("/api/update/status", ([FromServices] AgentUpdateService service) =>
        {
            return Results.Ok(service.GetStatus());
        });

        app.MapGet("/api/update/check", async ([FromServices] AgentUpdateService service, CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await service.CheckForUpdatesAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/update/start", async ([FromBody] StartAgentUpdateRequest? request, [FromServices] AgentUpdateService service) =>
        {
            try
            {
                var status = await service.StartUpdateAsync(request ?? new StartAgentUpdateRequest());
                return Results.Accepted("/api/update/status", status);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapGet("/api/files/roots", ([FromServices] FileService service) => Results.Ok(service.GetRoots()));

        app.MapGet("/api/files/space", (string? path, [FromServices] FileService service) =>
        {
            try
            {
                return Results.Ok(service.GetDriveSpace(path));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
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

        app.MapPost("/api/files/rename", ([FromBody] RenameEntryRequest request, [FromServices] FileService service) =>
        {
            try
            {
                service.RenameEntry(request.FullPath, request.NewName);
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

    private static string GetRegistryExportFileName(string path)
    {
        var rawName = string.IsNullOrWhiteSpace(path)
            ? "registry"
            : path.Replace('\\', '_');
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(rawName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return $"{sanitized}.reg";
    }

    private static async Task<string> ReadRegistryTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        if (HasPrefix(bytes, 0xFF, 0xFE))
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (HasPrefix(bytes, 0xFE, 0xFF))
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (HasPrefix(bytes, 0xEF, 0xBB, 0xBF))
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (LooksLikeUtf16Le(bytes))
        {
            return Encoding.Unicode.GetString(bytes);
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static bool HasPrefix(byte[] bytes, params byte[] prefix)
    {
        if (bytes.Length < prefix.Length)
        {
            return false;
        }

        for (var index = 0; index < prefix.Length; index++)
        {
            if (bytes[index] != prefix[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool LooksLikeUtf16Le(byte[] bytes)
    {
        if (bytes.Length < 4)
        {
            return false;
        }

        var pairsToInspect = Math.Min(bytes.Length / 2, 128);
        var zeroHighBytes = 0;
        for (var pairIndex = 0; pairIndex < pairsToInspect; pairIndex++)
        {
            if (bytes[(pairIndex * 2) + 1] == 0)
            {
                zeroHighBytes++;
            }
        }

        return zeroHighBytes >= pairsToInspect * 3 / 4;
    }
}
