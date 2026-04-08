using Microsoft.Win32;
using RemoteViewing.Vnc;
using StudentAgent.Services;

namespace StudentAgent.VncHost;

/// <summary>
/// Tries DXGI Desktop Duplication on a single monitor; stale DXGI frames (timeout) and runtime failures fall back to
/// GDI <see cref="DesktopCaptureFramebufferSource"/> with <see cref="InputDesktopGdiCapture"/> so Winlogon/UAC stay visible.
/// DXGI alone can throw after driver updates, session changes, or access loss — the original code only
/// caught failures at startup.
/// </summary>
internal sealed class HybridDesktopFramebufferSource : IVncFramebufferSource, IDisposable
{
    private static readonly TimeSpan[] DxgiRetryBackoff =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30)
    ];

    private readonly object _sync = new();
    private readonly AgentLogService _logService;
    private readonly DesktopCaptureFramebufferSource _gdi;
    private readonly bool _attemptDxgi;
    private DxgiDesktopFramebufferSource? _dxgi;
    private DateTimeOffset _nextDxgiRetryUtc = DateTimeOffset.MinValue;
    private int _dxgiFailureCount;
    private bool _disposed;

    public HybridDesktopFramebufferSource(AgentLogService logService, bool attemptDxgi)
    {
        _logService = logService;
        _gdi = new DesktopCaptureFramebufferSource(logService);
        _attemptDxgi = attemptDxgi;

        if (!attemptDxgi)
        {
            return;
        }

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        TryActivateDxgi("startup");
    }

    public bool SupportsResizing => false;

    public VncFramebuffer Capture()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_attemptDxgi && TryCaptureDxgi(out var framebuffer))
        {
            return framebuffer;
        }

        return _gdi.Capture();
    }

    public ExtendedDesktopSizeStatus SetDesktopSize(int width, int height)
        => ExtendedDesktopSizeStatus.Prohibited;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_attemptDxgi)
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
        }

        lock (_sync)
        {
            _dxgi?.DisposeDxgiResources();
            _dxgi = null;
        }
    }

    private static TimeSpan GetRetryDelay(int failureCount)
    {
        if (failureCount < 0)
        {
            return DxgiRetryBackoff[0];
        }

        return DxgiRetryBackoff[Math.Min(failureCount, DxgiRetryBackoff.Length - 1)];
    }

    private bool TryCaptureDxgi(out VncFramebuffer framebuffer)
    {
        framebuffer = null!;

        DxgiDesktopFramebufferSource? dxgi;
        lock (_sync)
        {
            dxgi = _dxgi;
            if (dxgi is null)
            {
                if (DateTimeOffset.UtcNow < _nextDxgiRetryUtc)
                {
                    return false;
                }

                dxgi = TryActivateDxgi("retry");
                if (dxgi is null)
                {
                    return false;
                }
            }
        }

        try
        {
            framebuffer = dxgi.Capture(out var timedOut);
            if (timedOut)
            {
                framebuffer = _gdi.Capture();
            }

            return true;
        }
        catch (Exception ex)
        {
            HandleDxgiFailure(ex);
            return false;
        }
    }

    private DxgiDesktopFramebufferSource? TryActivateDxgi(string reason)
    {
        lock (_sync)
        {
            if (_dxgi is not null || !_attemptDxgi || _disposed)
            {
                return _dxgi;
            }

            try
            {
                var dxgi = new DxgiDesktopFramebufferSource(_logService);
                _ = dxgi.Capture(out _);
                _dxgi = dxgi;
                _nextDxgiRetryUtc = DateTimeOffset.MinValue;

                if (_dxgiFailureCount == 0)
                {
                    _logService.LogInfo("VNC: DXGI Desktop Duplication active (GDI fallback if capture fails).");
                }
                else
                {
                    _logService.LogInfo($"VNC: DXGI Desktop Duplication recovered after GDI fallback ({reason}).");
                    _dxgiFailureCount = 0;
                }

                return _dxgi;
            }
            catch (Exception ex)
            {
                _dxgi = null;
                var retryDelay = GetRetryDelay(_dxgiFailureCount);
                _nextDxgiRetryUtc = DateTimeOffset.UtcNow.Add(retryDelay);
                _logService.LogWarning(
                    reason == "startup"
                        ? $"VNC: DXGI unavailable at startup ({ex.Message}); using GDI capture only."
                        : $"VNC: DXGI retry failed ({ex.Message}); next retry in {retryDelay.TotalSeconds:0}s.");
                _dxgiFailureCount++;
                return null;
            }
        }
    }

    private void HandleDxgiFailure(Exception ex)
    {
        lock (_sync)
        {
            _dxgi?.DisposeDxgiResources();
            _dxgi = null;
            var retryDelay = GetRetryDelay(_dxgiFailureCount);
            _nextDxgiRetryUtc = DateTimeOffset.UtcNow.Add(retryDelay);
            _dxgiFailureCount++;
            _logService.LogWarning(
                $"VNC: DXGI capture failed; using GDI temporarily ({ex.Message}). Next retry in {retryDelay.TotalSeconds:0}s.");
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        ResetDxgi("display settings changed");
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.ConsoleConnect:
            case SessionSwitchReason.ConsoleDisconnect:
            case SessionSwitchReason.RemoteConnect:
            case SessionSwitchReason.RemoteDisconnect:
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.SessionLogon:
            case SessionSwitchReason.SessionLogoff:
                ResetDxgi($"session switch ({e.Reason})");
                break;
        }
    }

    private void ResetDxgi(string reason)
    {
        lock (_sync)
        {
            if (_disposed || !_attemptDxgi)
            {
                return;
            }

            _dxgi?.ResetForDisplayChange();
            _dxgi = null;
            _nextDxgiRetryUtc = DateTimeOffset.UtcNow;
            _logService.LogInfo($"VNC: scheduling DXGI reinitialization after {reason}.");
        }
    }
}
