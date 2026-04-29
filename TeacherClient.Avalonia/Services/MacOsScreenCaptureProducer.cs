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

                // CGDisplayCreateImageForRect does not include the system pointer; draw a small
                // crosshair at the current mouse position so students see where the teacher is pointing.
                TryCompositeSystemMouseCursor(captureArea, width, height, bgraTight);

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

    /// <summary>
    /// CG display capture does not include the pointer. Composite a simple cursor overlay for classroom demo.
    /// </summary>
    private static void TryCompositeSystemMouseCursor(
        Rectangle captureArea,
        int width,
        int height,
        byte[] bgraTight)
    {
        if (bgraTight.Length < checked(width * height * 4))
        {
            return;
        }

        var displayId = CGMainDisplayID();
        if (displayId == 0)
        {
            return;
        }

        // Convert Quartz (origin bottom-left) mouse coordinates into a top-left coordinate
        // system that matches how the app chooses capture rectangles.
        var displayBounds = CGDisplayBounds(displayId);

        var ev = CGEventCreate(IntPtr.Zero);
        if (ev == IntPtr.Zero)
        {
            return;
        }

        CGPoint pt;
        try
        {
            pt = CGEventGetLocation(ev);
        }
        finally
        {
            CFRelease(ev);
        }

        // Quartz global coordinates: origin bottom-left. Convert to display-local top-left.
        var mouseXTopLeft = pt.X - displayBounds.X;
        var mouseYTopLeft = (displayBounds.Height - (pt.Y - displayBounds.Y));

        // captureArea is chosen in the same "top-left" space (0,0 at top-left of the main screen).
        // Map into capture-relative coordinates and scale into CGImage pixel space.
        var relX = mouseXTopLeft - captureArea.X;
        var relY = mouseYTopLeft - captureArea.Y;

        // Coordinate normalization:
        // - captureArea is in logical units (often points).
        // - CGImage dimensions are in pixels.
        var scaleX = captureArea.Width > 0 ? (double)width / captureArea.Width : 1.0;
        var scaleY = captureArea.Height > 0 ? (double)height / captureArea.Height : 1.0;

        var x = (int)Math.Round(relX * scaleX);
        var y = (int)Math.Round(relY * scaleY);
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }

        DrawArrowCursorBgra(bgraTight, width, height, x, y);
    }

    private static void DrawArrowCursorBgra(byte[] bgra, int w, int h, int tipX, int tipY)
    {
        void SetPixel(int px, int py, byte b, byte gr, byte r, byte a)
        {
            if (px < 0 || py < 0 || px >= w || py >= h)
            {
                return;
            }

            var o = (py * w + px) * 4;
            bgra[o] = b;
            bgra[o + 1] = gr;
            bgra[o + 2] = r;
            bgra[o + 3] = a;
        }

        // Simple arrow cursor:
        // - Tip at (tipX, tipY).
        // - White fill with black outline, similar to a standard pointer.
        const int arrowH = 18;
        const int arrowW = 12;

        // Outline (black) slightly larger than fill.
        for (var dy = 0; dy < arrowH; dy++)
        {
            var rowW = Math.Max(1, (arrowW * (arrowH - dy)) / arrowH);
            for (var dx = 0; dx < rowW; dx++)
            {
                SetPixel(tipX + dx, tipY + dy, 0, 0, 0, 255);
                SetPixel(tipX + dx + 1, tipY + dy, 0, 0, 0, 255);
                SetPixel(tipX + dx, tipY + dy + 1, 0, 0, 0, 255);
            }
        }

        // Fill (white) triangle.
        for (var dy = 0; dy < arrowH; dy++)
        {
            var rowW = Math.Max(1, (arrowW * (arrowH - dy)) / arrowH);
            for (var dx = 0; dx < rowW; dx++)
            {
                SetPixel(tipX + dx, tipY + dy, 255, 255, 255, 255);
            }
        }

        // Tail/stem.
        for (var dy = arrowH - 2; dy < arrowH + 10; dy++)
        {
            SetPixel(tipX + 3, tipY + dy, 0, 0, 0, 255);
            SetPixel(tipX + 4, tipY + dy, 255, 255, 255, 255);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CGPoint
    {
        public readonly double X;
        public readonly double Y;
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
    private static extern IntPtr CGEventCreate(IntPtr source);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern CGPoint CGEventGetLocation(IntPtr ev);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern CGRect CGDisplayBounds(uint display);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGPreflightScreenCaptureAccess();
}
