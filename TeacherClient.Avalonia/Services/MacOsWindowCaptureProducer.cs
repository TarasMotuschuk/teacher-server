using System.Diagnostics;
using System.Runtime.InteropServices;

using SIPSorceryMedia.Abstractions;

namespace TeacherClient.CrossPlatform.Services;

public sealed class MacOsWindowCaptureProducer : IDisposable
{
    private readonly object _sync = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public void Start(long windowId, int captureFps, Action<uint, int, int, byte[], VideoPixelFormatsEnum> onFrame)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException($"{nameof(MacOsWindowCaptureProducer)} can only run on macOS.");
        }

        if (windowId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowId));
        }

        if (onFrame is null)
        {
            throw new ArgumentNullException(nameof(onFrame));
        }

        MacOsScreenCaptureProducer.EnsureScreenCaptureAccess();

        lock (_sync)
        {
            if (_cts is not null)
            {
                return;
            }

            var fps = Math.Clamp(captureFps, 1, 60);
            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => CaptureLoop(windowId, fps, onFrame, _cts.Token));
        }
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? loop;
        lock (_sync)
        {
            cts = _cts;
            loop = _loopTask;
            _cts = null;
            _loopTask = null;
        }

        if (cts is null)
        {
            return;
        }

        try { cts.Cancel(); } catch { }
        finally { cts.Dispose(); }

        if (loop is not null)
        {
            try { await loop.ConfigureAwait(false); } catch { }
        }
    }

    public void Dispose()
    {
        _ = StopAsync();
    }

    private static async Task CaptureLoop(
        long windowId,
        int fps,
        Action<uint, int, int, byte[], VideoPixelFormatsEnum> onFrame,
        CancellationToken ct)
    {
        var frameDurationMs = (uint)Math.Max(1, (int)Math.Round(1000.0 / fps));
        var sw = Stopwatch.StartNew();
        var nextTickMs = 0L;

        while (!ct.IsCancellationRequested)
        {
            var now = sw.ElapsedMilliseconds;
            var delay = nextTickMs - now;
            if (delay > 0)
            {
                try { await Task.Delay((int)Math.Min(int.MaxValue, delay), ct).ConfigureAwait(false); }
                catch { break; }
            }

            nextTickMs += frameDurationMs;

            try
            {
                if (TryCaptureWindowBgra(windowId, out var width, out var height, out var bgra))
                {
                    onFrame(frameDurationMs, width, height, bgra, VideoPixelFormatsEnum.Bgra);
                }
            }
            catch
            {
                // Best-effort: capture loop should not throw.
            }
        }
    }

    private static bool TryCaptureWindowBgra(long windowId, out int width, out int height, out byte[] bgraTight)
    {
        width = 0;
        height = 0;
        bgraTight = Array.Empty<byte>();

        // NOTE: This uses CoreGraphics window image capture, not ScreenCaptureKit.
        // It is sufficient for many classroom scenarios; if we need guaranteed capture
        // of occluded windows with better performance, we can upgrade to ScreenCaptureKit.
        var rect = CGRect.Null;
        var image = CGWindowListCreateImage(
            rect,
            CGWindowListOption.IncludingWindow,
            (uint)windowId,
            CGWindowImageOption.BestResolution);
        if (image == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            width = checked((int)CGImageGetWidth(image));
            height = checked((int)CGImageGetHeight(image));
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            var bytesPerRow = checked((int)CGImageGetBytesPerRow(image));
            var expectedTightRow = checked(width * 4);
            if (bytesPerRow < expectedTightRow)
            {
                return false;
            }

            var provider = CGImageGetDataProvider(image);
            if (provider == IntPtr.Zero)
            {
                return false;
            }

            var data = CGDataProviderCopyData(provider);
            if (data == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                var len = (nint)CFDataGetLength(data);
                var ptr = CFDataGetBytePtr(data);
                if (ptr == IntPtr.Zero || len <= 0)
                {
                    return false;
                }

                bgraTight = new byte[checked(expectedTightRow * height)];
                for (var y = 0; y < height; y++)
                {
                    Marshal.Copy(
                        IntPtr.Add(ptr, y * bytesPerRow),
                        bgraTight,
                        y * expectedTightRow,
                        expectedTightRow);
                }

                return true;
            }
            finally
            {
                CFRelease(data);
            }
        }
        finally
        {
            CGImageRelease(image);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CGRect(double x, double y, double width, double height)
    {
        public static CGRect Null => new(0, 0, 0, 0);

        public readonly double X = x;
        public readonly double Y = y;
        public readonly double Width = width;
        public readonly double Height = height;
    }

    [Flags]
    private enum CGWindowListOption : uint
    {
        IncludingWindow = 1 << 3,
    }

    [Flags]
    private enum CGWindowImageOption : uint
    {
        BestResolution = 1 << 1,
    }

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGWindowListCreateImage(
        CGRect screenBounds,
        CGWindowListOption listOption,
        uint windowID,
        CGWindowImageOption imageOption);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern nuint CGImageGetWidth(IntPtr image);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern nuint CGImageGetHeight(IntPtr image);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern nuint CGImageGetBytesPerRow(IntPtr image);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGImageGetDataProvider(IntPtr image);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGDataProviderCopyData(IntPtr provider);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGImageRelease(IntPtr image);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern nint CFDataGetLength(IntPtr data);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFDataGetBytePtr(IntPtr data);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);
}

