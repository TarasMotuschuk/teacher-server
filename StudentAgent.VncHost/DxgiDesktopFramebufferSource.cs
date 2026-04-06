using System.Drawing;
using System.Runtime.InteropServices;
using RemoteViewing.Vnc;
using RemoteViewing.Vnc.Server;
using StudentAgent.Services;
using StudentAgent.UI.Localization;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
namespace StudentAgent.VncHost;

/// <summary>
/// DXGI Desktop Duplication — same technique class as Veyon's bundled UltraVNC
/// (<c>DeskdupEngine.cpp</c>, <c>_USE_DESKTOPDUPLICATION</c>). Duplicates the GPU-composed image,
/// so secure-desktop UAC prompts match what Veyon shows. GDI <c>CopyFromScreen</c> does not.
/// </summary>
internal sealed class DxgiDesktopFramebufferSource : IVncFramebufferSource
{
    private const int DxgiErrorAccessLost = unchecked((int)0x887A0026);
    private const int DxgiErrorWaitTimeout = unchecked((int)0x887A0027);
    private const int DxgiErrorInvalidCall = unchecked((int)0x887A0001);

    private readonly object _sync = new();
    private readonly AgentLogService _logService;

    private IDXGIAdapter1? _adapter;
    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGIOutputDuplication? _duplication;
    private IDXGIOutput1? _output1;
    private ID3D11Texture2D? _staging;
    private VncFramebuffer? _framebuffer;
    private int _width;
    private int _height;
    private bool _loggedInit;
    private int _accessLostCount;
    private int _invalidCallCount;
    private int _timeoutCount;

    public DxgiDesktopFramebufferSource(AgentLogService logService)
    {
        _logService = logService;
    }

    public bool SupportsResizing => false;

    public VncFramebuffer Capture()
    {
        lock (_sync)
        {
            if (!EnsureInitialized())
            {
                throw new InvalidOperationException("DXGI desktop duplication is not available.");
            }

            var duplication = _duplication!;
            var staging = _staging!;
            var framebuffer = _framebuffer!;
            var context = _context!;

            var acquired = false;
            IDXGIResource? desktopResource = null;
            try
            {
                var hr = duplication.AcquireNextFrame(100, out _, out desktopResource);
                var code = hr.Code;
                if (code == DxgiErrorWaitTimeout)
                {
                    _timeoutCount++;
                    return framebuffer;
                }

                if (code == DxgiErrorAccessLost || code == DxgiErrorInvalidCall)
                {
                    if (code == DxgiErrorAccessLost)
                    {
                        _accessLostCount++;
                    }
                    else
                    {
                        _invalidCallCount++;
                    }

                    TeardownDuplication();
                    throw new InvalidOperationException(
                        code == DxgiErrorAccessLost
                            ? "DXGI duplication access was lost."
                            : "DXGI duplication reported an invalid call.");
                }

                if (hr.Failure || desktopResource is null)
                {
                    throw new InvalidOperationException($"DXGI AcquireNextFrame failed: 0x{code:X8}");
                }

                acquired = true;

                using var src = desktopResource.QueryInterface<ID3D11Texture2D>();
                context.CopyResource(staging, src);

                var mapped = context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                try
                {
                    var buffer = framebuffer.GetBuffer();
                    lock (framebuffer.SyncRoot)
                    {
                        var dstStride = framebuffer.Stride;
                        var rowBytes = _width * 4;
                        var srcPitch = mapped.RowPitch;
                        var srcPitchInt = (int)srcPitch;
                        for (var y = 0; y < _height; y++)
                        {
                            Marshal.Copy(
                                IntPtr.Add(mapped.DataPointer, y * srcPitchInt),
                                buffer,
                                y * dstStride,
                                rowBytes);
                        }
                    }
                }
                finally
                {
                    context.Unmap(staging, 0);
                }
            }
            finally
            {
                if (acquired)
                {
                    try
                    {
                        duplication.ReleaseFrame();
                    }
                    catch
                    {
                    }
                }

                desktopResource?.Dispose();
            }

            return framebuffer;
        }
    }

    public ExtendedDesktopSizeStatus SetDesktopSize(int width, int height)
        => ExtendedDesktopSizeStatus.Prohibited;

    /// <summary>Releases DXGI/D3D objects when switching to GDI-only fallback.</summary>
    internal void DisposeDxgiResources()
    {
        lock (_sync)
        {
            TeardownDuplication();
        }
    }

    internal void ResetForDisplayChange()
    {
        lock (_sync)
        {
            TeardownDuplication();
        }
    }

    private bool EnsureInitialized()
    {
        if (_duplication is not null && _staging is not null && _device is not null)
        {
            return true;
        }

        TeardownDuplication();

        try
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            if (!TryFindPrimaryOutput(factory, out var adapter, out var output1, out var desktopLeft, out var desktopTop, out var desktopRight, out var desktopBottom))
            {
                return false;
            }

            _adapter = adapter;
            _output1 = output1;

            var w = desktopRight - desktopLeft;
            var h = desktopBottom - desktopTop;
            if (w < 1 || h < 1)
            {
                return false;
            }

            _width = w;
            _height = h;

            var levels = new[]
            {
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_1,
                FeatureLevel.Level_10_0,
            };

            if (D3D11.D3D11CreateDevice(
                    adapter,
                    DriverType.Unknown,
                    DeviceCreationFlags.VideoSupport | DeviceCreationFlags.BgraSupport,
                    levels,
                    out _device,
                    out _,
                    out _context).Failure || _device is null || _context is null)
            {
                return false;
            }

            _duplication = output1.DuplicateOutput(_device);

            _staging = _device.CreateTexture2D(new Texture2DDescription(
                Format.B8G8R8A8_UNorm,
                (uint)_width,
                (uint)_height,
                arraySize: 1,
                mipLevels: 1,
                bindFlags: BindFlags.None,
                usage: ResourceUsage.Staging,
                cpuAccessFlags: CpuAccessFlags.Read,
                sampleCount: 1,
                sampleQuality: 0,
                miscFlags: ResourceOptionFlags.None));

            _framebuffer = new VncFramebuffer(
                StudentAgentText.AgentName,
                _width,
                _height,
                VncPixelFormat.RGB32);

            if (!_loggedInit)
            {
                _loggedInit = true;
                _logService.LogInfo(
                    $"VNC desktop capture: DXGI Desktop Duplication ({_width}x{_height}, primary output).");
            }
            else if (_accessLostCount > 0 || _invalidCallCount > 0)
            {
                _logService.LogInfo(
                    $"VNC desktop capture: DXGI duplication recovered ({_width}x{_height}, accessLost={_accessLostCount}, invalidCall={_invalidCallCount}, timeouts={_timeoutCount}).");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"VNC DXGI init failed: {ex.Message}");
            TeardownDuplication();
            return false;
        }
    }

    private void TeardownDuplication()
    {
        _staging?.Dispose();
        _staging = null;
        try
        {
            _duplication?.Dispose();
        }
        catch
        {
        }

        _duplication = null;
        _output1?.Dispose();
        _output1 = null;
        _context?.Dispose();
        _context = null;
        _device?.Dispose();
        _device = null;
        _adapter?.Dispose();
        _adapter = null;
        _framebuffer = null;
    }

    private static bool TryFindPrimaryOutput(
        IDXGIFactory1 factory,
        out IDXGIAdapter1 adapter,
        out IDXGIOutput1 output1,
        out int desktopLeft,
        out int desktopTop,
        out int desktopRight,
        out int desktopBottom)
    {
        adapter = null!;
        output1 = null!;
        desktopLeft = desktopTop = desktopRight = desktopBottom = 0;

        var primary = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        var cx = primary.Left + Math.Max(1, primary.Width) / 2;
        var cy = primary.Top + Math.Max(1, primary.Height) / 2;

        for (uint i = 0; ; i++)
        {
            if (factory.EnumAdapters1(i, out var a).Failure)
            {
                break;
            }

            var matched = false;
            for (uint j = 0; ; j++)
            {
                if (a.EnumOutputs(j, out var output).Failure)
                {
                    break;
                }

                try
                {
                    var desc = output.Description;
                    var r = desc.DesktopCoordinates;
                    if (cx >= r.Left && cx < r.Right && cy >= r.Top && cy < r.Bottom)
                    {
                        output1 = output.QueryInterface<IDXGIOutput1>();
                        adapter = a;
                        desktopLeft = r.Left;
                        desktopTop = r.Top;
                        desktopRight = r.Right;
                        desktopBottom = r.Bottom;
                        matched = true;
                        return true;
                    }
                }
                finally
                {
                    output.Dispose();
                }
            }

            if (!matched)
            {
                a.Dispose();
            }
        }

        return false;
    }
}
