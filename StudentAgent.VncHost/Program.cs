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
    }, stoppingToken);
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

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
        => NullScope.Instance;

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

internal sealed class WindowsVncRemoteKeyboard : IVncRemoteKeyboard
{
    private readonly AgentLogService _logService;
    private int _sendInputFailureLogBudget = 8;
    private int _ctrlDownCount;
    private int _altDownCount;
    private bool _skipNextDeleteKeyUp;

    public WindowsVncRemoteKeyboard(AgentLogService logService)
    {
        _logService = logService;
    }

    public void HandleKeyEvent(object? sender, KeyChangedEventArgs e)
    {
        try
        {
            var keysymU = (uint)e.Keysym;
            UpdateModifierTally(keysymU, e.Pressed);

            // Windows blocks synthetic Ctrl+Alt+Del (SAS). Open Task Manager instead — usual classroom need.
            if (keysymU == 0xFFFF && e.Pressed && _ctrlDownCount > 0 && _altDownCount > 0 &&
                TryLaunchTaskManagerFromCadShortcut())
            {
                _skipNextDeleteKeyUp = true;
                return;
            }

            if (keysymU == 0xFFFF && !e.Pressed && _skipNextDeleteKeyUp)
            {
                _skipNextDeleteKeyUp = false;
                return;
            }

            if (TryMapVirtualKey(e.Keysym, out var virtualKey, out var scanCode, out var keyFlags))
            {
                SendVirtualKey(virtualKey, scanCode, e.Pressed, keyFlags, (uint)e.Keysym);
                return;
            }

            var keysymValue = (uint)e.Keysym;
            if (keysymValue is >= 0x20 and <= 0xFFFF)
            {
                SendUnicode((char)keysymValue, e.Pressed);
            }
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"VNC keyboard event failed: {ex.Message}");
        }
    }

    private void UpdateModifierTally(uint keysymU, bool pressed)
    {
        var delta = pressed ? 1 : -1;
        if (keysymU is 0xFFE3 or 0xFFE4)
        {
            _ctrlDownCount = Math.Max(0, _ctrlDownCount + delta);
        }
        else if (keysymU is 0xFFE9 or 0xFFEA)
        {
            _altDownCount = Math.Max(0, _altDownCount + delta);
        }
    }

    private bool TryLaunchTaskManagerFromCadShortcut()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "taskmgr.exe",
                UseShellExecute = true,
            });
            return true;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"VNC Ctrl+Alt+Del shortcut: could not start Task Manager: {ex.Message}");
            return false;
        }
    }

    private static bool TryMapVirtualKey(KeySym keySym, out ushort virtualKey, out ushort scanCode, out uint keyFlags)
    {
        virtualKey = 0;
        scanCode = 0;
        keyFlags = 0;

        switch ((uint)keySym)
        {
            case 0xFF08:
                virtualKey = 0x08;
                return true;
            case 0xFF09:
                virtualKey = 0x09;
                return true;
            case 0xFF0D:
                virtualKey = 0x0D;
                return true;
            case 0xFF1B:
                virtualKey = 0x1B;
                return true;
            case 0xFF50:
                virtualKey = 0x24;
                keyFlags = KEYEVENTFEXTENDEDKEY;
                return true;
            case 0xFF51:
                virtualKey = 0x25;
                keyFlags = KEYEVENTFEXTENDEDKEY;
                return true;
            case 0xFF52:
                virtualKey = 0x26;
                keyFlags = KEYEVENTFEXTENDEDKEY;
                return true;
            case 0xFF53:
                virtualKey = 0x27;
                keyFlags = KEYEVENTFEXTENDEDKEY;
                return true;
            case 0xFF54:
                virtualKey = 0x28;
                keyFlags = KEYEVENTFEXTENDEDKEY;
                return true;
            case 0xFF55:
                virtualKey = 0x21;
                keyFlags = KEYEVENTFEXTENDEDKEY;
                return true;
            case 0xFF56:
                virtualKey = 0x22;
                keyFlags = KEYEVENTFEXTENDEDKEY;
                return true;
            case 0xFF57:
                virtualKey = 0x23;
                keyFlags = KEYEVENTFEXTENDEDKEY;
                return true;
            case 0xFF63:
                virtualKey = 0x2D;
                keyFlags = KEYEVENTFEXTENDEDKEY;
                return true;
            case 0xFFFF:
                virtualKey = 0x2E;
                keyFlags = KEYEVENTFEXTENDEDKEY;
                return true;
            case 0xFFE1:
                virtualKey = 0xA0;
                return true;
            case 0xFFE2:
                virtualKey = 0xA1;
                return true;
            case 0xFFE3:
                virtualKey = 0xA2;
                return true;
            case 0xFFE4:
                virtualKey = 0xA3;
                keyFlags = KEYEVENTFEXTENDEDKEY;
                return true;
            case 0xFFE9:
                virtualKey = 0xA4;
                return true;
            case 0xFFEA:
                virtualKey = 0xA5;
                keyFlags = KEYEVENTFEXTENDEDKEY;
                return true;

            // Left/Right Win — X11 Super_* / Meta_* (MarcusW KeySymbol.Super_L etc.)
            case 0xFFE7:
            case 0xFFEB:
                virtualKey = 0x5B;
                return true;
            case 0xFFE8:
            case 0xFFEC:
                virtualKey = 0x5C;
                return true;
        }

        var raw = (uint)keySym;
        if (raw is >= 0xFFBE and <= 0xFFC9)
        {
            virtualKey = (ushort)(0x70 + (raw - 0xFFBE));
            return true;
        }

        if (raw < 0x20)
        {
            return false;
        }

        return false;
    }

    private void SendVirtualKey(ushort virtualKey, ushort scanCode, bool pressed, uint keyFlags, uint keysymForLog)
    {
        var input = new INPUT
        {
            Type = INPUTKEYBOARD,
            U = new InputUnion
            {
                Ki = new KEYBDINPUT
                {
                    WVk = virtualKey,
                    WScan = scanCode,
                    DwFlags = keyFlags | (pressed ? 0u : KEYEVENTFKEYUP),
                    DwExtraInfo = UIntPtr.Zero
                }
            },
        };

        SendSingleInput(input, keysymForLog);
    }

    private void SendUnicode(char character, bool pressed)
    {
        var input = new INPUT
        {
            Type = INPUTKEYBOARD,
            U = new InputUnion
            {
                Ki = new KEYBDINPUT
                {
                    WVk = 0,
                    WScan = character,
                    DwFlags = KEYEVENTFUNICODE | (pressed ? 0u : KEYEVENTFKEYUP),
                    DwExtraInfo = UIntPtr.Zero
                }
            },
        };

        SendSingleInput(input, character);
    }

    private void SendSingleInput(INPUT input, uint keysymForLog)
    {
        var inputs = new[] { input };
        var size = Marshal.SizeOf<INPUT>();
        var sent = SendInput((uint)inputs.Length, inputs, size);
        if (sent != 0)
        {
            return;
        }

        if (_sendInputFailureLogBudget-- <= 0)
        {
            return;
        }

        _logService.LogWarning(
            $"VNC SendInput failed (keysym=0x{keysymForLog:X}, cbSize={size}, err={Marshal.GetLastWin32Error()}).");
    }

    private const uint INPUTKEYBOARD = 1;
    private const uint KEYEVENTFEXTENDEDKEY = 0x0001;
    private const uint KEYEVENTFKEYUP = 0x0002;
    private const uint KEYEVENTFUNICODE = 0x0004;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    /// <summary>
    /// Must match Win32 INPUT: the union size is that of the largest member (MOUSEINPUT), or SendInput rejects keyboard events on x64.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT Mi;
        [FieldOffset(0)]
        public KEYBDINPUT Ki;
        [FieldOffset(0)]
        public HARDWAREINPUT Hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint DwFlags;
        public uint Time;
        public UIntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint UMsg;
        public ushort WParamL;
        public ushort WParamH;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort WVk;
        public ushort WScan;
        public uint DwFlags;
        public uint Time;
        public UIntPtr DwExtraInfo;
    }
}

internal sealed class WindowsVncRemoteController : IVncRemoteController
{
    private readonly VncMouse _mouse = new();

    public void HandleTouchEvent(object? sender, PointerChangedEventArgs e)
    {
        _mouse.OnMouseUpdate(sender, e);
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
                InputDesktopGdiCapture.CopyVirtualScreenToBitmap(bounds, width, height, bitmap);

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
