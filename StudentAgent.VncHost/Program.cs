using System.Diagnostics;
using System.Drawing.Imaging;
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteViewing.Hosting;
using RemoteViewing.Vnc;
using RemoteViewing.Vnc.Server;
using StudentAgent;
using StudentAgent.Services;
using StudentAgent.UI.Localization;
using StudentAgent.VncHost;
using KeySym = RemoteViewing.Vnc.KeySym;

try
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .Build();

    var agentOptions = configuration.GetSection(AgentOptions.SectionName).Get<AgentOptions>() ?? new AgentOptions();
    var logService = new AgentLogService();
    var settingsStore = new AgentSettingsStore(Options.Create(agentOptions));

    AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
    {
        logService.LogError($"Unhandled VNC host exception: {eventArgs.ExceptionObject}");
    };
    TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
    {
        logService.LogError($"Unobserved VNC host task exception: {eventArgs.Exception}");
        eventArgs.SetObserved();
    };

    StudentAgentText.SetLanguage(settingsStore.Current.Language);

    var settings = settingsStore.Current;
    if (!settings.VncEnabled)
    {
        logService.LogInfo("StudentAgent.VncHost is disabled in settings.");
        return;
    }

    var source = CreateFramebufferSource(logService);
    var keyboard = new WindowsVncRemoteKeyboard(logService);
    var controller = new WindowsVncRemoteController();

    var builder = Host.CreateApplicationBuilder(args);
    builder.Logging.ClearProviders();
    builder.Logging.AddProvider(new AgentLogLoggerProvider(logService));
    builder.Services.AddSingleton(logService);
    builder.Services.AddSingleton(settingsStore);
    builder.Services.AddSingleton<IVncFramebufferSource>(source);
    builder.Services.AddSingleton<IVncRemoteKeyboard>(keyboard);
    builder.Services.AddSingleton<IVncRemoteController>(controller);
    builder.Services.AddVncServer<VncServer>(new VncServerOptions
    {
        Address = IPAddress.Any.ToString(),
        Port = Math.Max(1, settings.VncPort),
        Password = settings.VncPassword,
        Reverse = false,
    });

    using var host = builder.Build();
    WireServerDiagnostics(
        host.Services.GetRequiredService<IVncServer>(),
        logService,
        host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
    logService.LogInfo($"Starting VNC host on {IPAddress.Any}:{Math.Max(1, settings.VncPort)} (view-only: {settings.VncViewOnly}).");
    await host.RunAsync();
}
catch (Exception ex)
{
    var startupLogPath = Path.Combine(StudentAgentPathHelper.GetLogsDirectory(), "studentagent-vnchost-startup-error.log");
    Directory.CreateDirectory(Path.GetDirectoryName(startupLogPath)!);
    File.WriteAllText(startupLogPath, ex.ToString());
}

static IVncFramebufferSource CreateFramebufferSource(AgentLogService log)
{
    if (SystemInformation.MonitorCount > 1)
    {
        log.LogInfo(
            "VNC: multiple monitors — GDI capture for the full virtual desktop.");
        return new DesktopCaptureFramebufferSource(log);
    }

    return new HybridDesktopFramebufferSource(log, attemptDxgi: true);
}

static void WireServerDiagnostics(IVncServer server, AgentLogService logService, CancellationToken stoppingToken)
{
    server.Connected += (_, _) =>
    {
        logService.LogInfo($"VNC server connected. Active sessions: {server.Sessions.Count}.");
    };
    server.Closed += (_, _) =>
    {
        logService.LogInfo($"VNC server closed. Active sessions: {server.Sessions.Count}.");
    };
    server.PasswordProvided += (_, eventArgs) =>
    {
        logService.LogInfo($"VNC server password provided. Authenticated: {eventArgs.IsAuthenticated}.");
    };

    _ = Task.Run(
        async () =>
        {
            var knownSessions = new HashSet<IVncServerSession>();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    foreach (var session in server.Sessions.ToList())
                    {
                        if (!knownSessions.Add(session))
                        {
                            continue;
                        }

                        session.Connected += (_, _) =>
                        {
                            logService.LogInfo("VNC session connected.");
                        };
                        session.ConnectionFailed += (_, _) =>
                        {
                            logService.LogWarning("VNC session connection failed.");
                        };
                        session.Closed += (_, _) =>
                        {
                            logService.LogInfo("VNC session closed.");
                        };
                        session.PasswordProvided += (_, eventArgs) =>
                        {
                            logService.LogInfo($"VNC session password provided. Authenticated: {eventArgs.IsAuthenticated}.");
                        };
                    }
                }
                catch (Exception ex)
                {
                    logService.LogWarning($"Failed to inspect VNC sessions: {ex.Message}");
                }

                try
                {
                    await Task.Delay(500, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        },
        stoppingToken);
}
