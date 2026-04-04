using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Runtime.InteropServices;
using RemoteViewing.Hosting;
using RemoteViewing.Vnc;
using RemoteViewing.Vnc.Server;
using StudentAgent;
using StudentAgent.Services;
using StudentAgent.UI.Localization;
using System.Windows.Forms;

try
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .Build();

    var agentOptions = configuration.GetSection(AgentOptions.SectionName).Get<AgentOptions>() ?? new AgentOptions();
    var logService = new AgentLogService();
    var settingsStore = new AgentSettingsStore(Options.Create(agentOptions));

    StudentAgentText.SetLanguage(settingsStore.Current.Language);

    var settings = settingsStore.Current;
    if (!settings.VncEnabled)
    {
        logService.LogInfo("StudentAgent.VncHost is disabled in settings.");
        return;
    }

    var source = new DesktopCaptureFramebufferSource(logService);
    var keyboard = new NoOpVncRemoteKeyboard();
    var controller = new NoOpVncRemoteController();

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
        Reverse = false
    });

    using var host = builder.Build();
    logService.LogInfo($"Starting VNC host on {IPAddress.Any}:{Math.Max(1, settings.VncPort)} (view-only: {settings.VncViewOnly}).");
    await host.RunAsync();
}
catch (Exception ex)
{
    var startupLogPath = Path.Combine(StudentAgentPathHelper.GetLogsDirectory(), "studentagent-vnchost-startup-error.log");
    Directory.CreateDirectory(Path.GetDirectoryName(startupLogPath)!);
    File.WriteAllText(startupLogPath, ex.ToString());
}

internal sealed class AgentLogLoggerProvider : ILoggerProvider
{
    private readonly AgentLogService _logService;

    public AgentLogLoggerProvider(AgentLogService logService)
    {
        _logService = logService;
    }

    public ILogger CreateLogger(string categoryName) => new AgentLogLogger(_logService, categoryName);

    public void Dispose()
    {
    }
}

internal sealed class AgentLogLogger : ILogger
{
    private readonly AgentLogService _logService;
    private readonly string _categoryName;

    public AgentLogLogger(AgentLogService logService, string categoryName)
    {
        _logService = logService;
        _categoryName = categoryName;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        if (string.IsNullOrWhiteSpace(message) && exception is null)
        {
            return;
        }

        var fullMessage = $"[{_categoryName}] {message}";
        if (exception is not null)
        {
            fullMessage = $"{fullMessage}{Environment.NewLine}{exception}";
        }

        switch (logLevel)
        {
            case LogLevel.Warning:
                _logService.LogWarning(fullMessage);
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                _logService.LogError(fullMessage);
                break;
            default:
                _logService.LogInfo(fullMessage);
                break;
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

internal sealed class NoOpVncRemoteKeyboard : IVncRemoteKeyboard
{
    public void HandleKeyEvent(object? sender, KeyChangedEventArgs e)
    {
    }
}

internal sealed class NoOpVncRemoteController : IVncRemoteController
{
    public void HandleTouchEvent(object? sender, PointerChangedEventArgs e)
    {
    }
}

internal sealed class DesktopCaptureFramebufferSource : IVncFramebufferSource
{
    private readonly object _sync = new();
    private readonly AgentLogService _logService;
    private VncFramebuffer? _framebuffer;
    private int _width;
    private int _height;

    public DesktopCaptureFramebufferSource(AgentLogService logService)
    {
        _logService = logService;
    }

    public bool SupportsResizing => false;

    public VncFramebuffer Capture()
    {
        lock (_sync)
        {
            var bounds = SystemInformation.VirtualScreen;
            var width = Math.Max(1, bounds.Width);
            var height = Math.Max(1, bounds.Height);

            if (_framebuffer is null || _width != width || _height != height)
            {
                _width = width;
                _height = height;
                _framebuffer = new VncFramebuffer(
                    StudentAgentText.AgentName,
                    width,
                    height,
                    VncPixelFormat.RGB32);
            }

            try
            {
                using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
                }

                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppRgb);

                try
                {
                    var buffer = _framebuffer.GetBuffer();
                    lock (_framebuffer.SyncRoot)
                    {
                        var sourceStride = Math.Abs(bitmapData.Stride);
                        var targetStride = _framebuffer.Stride;
                        var bytesPerRow = Math.Min(sourceStride, targetStride);

                        for (var row = 0; row < height; row++)
                        {
                            var sourceRow = IntPtr.Add(bitmapData.Scan0, row * bitmapData.Stride);
                            Marshal.Copy(sourceRow, buffer, row * targetStride, bytesPerRow);
                        }
                    }
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"VNC desktop capture failed: {ex.Message}");
            }

            return _framebuffer;
        }
    }

    public ExtendedDesktopSizeStatus SetDesktopSize(int width, int height)
        => ExtendedDesktopSizeStatus.Prohibited;
}
