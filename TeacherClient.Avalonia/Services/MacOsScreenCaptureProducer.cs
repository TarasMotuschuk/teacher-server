using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

using SIPSorceryMedia.Abstractions;

namespace TeacherClient.CrossPlatform.Services;

public sealed class MacOsScreenCaptureProducer : IDisposable
{
    private readonly object _sync = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public static bool HasScreenCaptureAccess()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        return CGPreflightScreenCaptureAccess();
    }

    public static void EnsureScreenCaptureAccess()
    {
        if (!HasScreenCaptureAccess())
        {
            throw new InvalidOperationException(
                "macOS Screen Recording permission is not granted for ClassCommander. " +
                "Open System Settings -> Privacy & Security -> Screen Recording, enable ClassCommander, then relaunch the app.");
        }
    }

    public void Start(Rectangle captureArea, int captureFps, Action<uint, int, int, byte[], VideoPixelFormatsEnum> onFrame)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("MacOsScreenCaptureProducer can only run on macOS.");
        }

        if (onFrame is null)
        {
            throw new ArgumentNullException(nameof(onFrame));
        }

        EnsureScreenCaptureAccess();

        lock (_sync)
        {
            if (_cts is not null)
            {
                return;
            }

            var fps = Math.Clamp(captureFps, 1, 60);
            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => CaptureLoop(captureArea, fps, onFrame, _cts.Token));
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

        try
        {
            cts.Cancel();
        }
        catch
        {
        }
        finally
        {
            cts.Dispose();
        }

        if (loop is not null)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    public void Dispose()
    {
        _ = StopAsync();
    }

    private static async Task CaptureLoop(
        Rectangle captureArea,
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
                try
                {
                    await Task.Delay((int)Math.Min(int.MaxValue, delay), ct).ConfigureAwait(false);
                }
                catch
                {
                    break;
                }
            }

            nextTickMs += frameDurationMs;

            try
            {
                if (TryCaptureBgra(captureArea, out var width, out var height, out var bgra))
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

    private static bool TryCaptureBgra(Rectangle captureArea, out int width, out int height, out byte[] bgraTight)
    {
        width = 0;
        height = 0;
        bgraTight = Array.Empty<byte>();

        var displayId = CGMainDisplayID();
        if (displayId == 0)
        {
            return false;
        }

        var rect = new CGRect(captureArea.X, captureArea.Y, Math.Max(1, captureArea.Width), Math.Max(1, captureArea.Height));
        var image = CGDisplayCreateImageForRect(displayId, rect);
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
        public readonly double X = x;
        public readonly double Y = y;
        public readonly double Width = width;
        public readonly double Height = height;
    }

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern uint CGMainDisplayID();

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGDisplayCreateImageForRect(uint display, CGRect rect);

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

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern nint CFDataGetLength(IntPtr data);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFDataGetBytePtr(IntPtr data);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGImageRelease(IntPtr image);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGPreflightScreenCaptureAccess();
}
