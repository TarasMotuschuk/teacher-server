using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using SIPSorceryMedia.Abstractions;

namespace TeacherClient.CrossPlatform.Services;

public sealed class WindowsWindowCaptureProducer : IDisposable
{
    private readonly object _sync = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public void Start(nint hwnd, int captureFps, Action<uint, int, int, byte[], VideoPixelFormatsEnum> onFrame)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException($"{nameof(WindowsWindowCaptureProducer)} can only run on Windows.");
        }

        if (hwnd == nint.Zero)
        {
            throw new ArgumentException("HWND is null.", nameof(hwnd));
        }

        if (onFrame is null)
        {
            throw new ArgumentNullException(nameof(onFrame));
        }

        lock (_sync)
        {
            if (_cts is not null)
            {
                return;
            }

            var fps = Math.Clamp(captureFps, 1, 60);
            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => CaptureLoop(hwnd, fps, onFrame, _cts.Token));
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
        nint hwnd,
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
                if (TryCaptureWindowBgra(hwnd, out var w, out var h, out var bgra))
                {
                    onFrame(frameDurationMs, w, h, bgra, VideoPixelFormatsEnum.Bgra);
                }
            }
            catch
            {
                // Best-effort: capture loop should not throw.
            }
        }
    }

    private static bool TryCaptureWindowBgra(nint hwnd, out int width, out int height, out byte[] bgraTight)
    {
        width = 0;
        height = 0;
        bgraTight = [];

        if (!GetWindowRect(hwnd, out var rect))
        {
            return false;
        }

        width = Math.Max(1, rect.Right - rect.Left);
        height = Math.Max(1, rect.Bottom - rect.Top);

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var gfx = Graphics.FromImage(bitmap);

        var hdc = gfx.GetHdc();
        try
        {
            // PW_RENDERFULLCONTENT gives better results on modern Windows; if it fails, fall back.
            if (!PrintWindow(hwnd, hdc, 0x00000002))
            {
                if (!PrintWindow(hwnd, hdc, 0))
                {
                    gfx.ReleaseHdc(hdc);
                    try
                    {
                        // Fallback: copy from screen (will include occlusion).
                        using var g2 = Graphics.FromImage(bitmap);
                        g2.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
                    }
                    catch
                    {
                        return false;
                    }
                    return TryGetBgraTight(bitmap, out bgraTight);
                }
            }
        }
        finally
        {
            try
            {
                gfx.ReleaseHdc(hdc);
            }
            catch
            {
            }
        }

        return TryGetBgraTight(bitmap, out bgraTight);
    }

    private static bool TryGetBgraTight(Bitmap bitmap, out byte[] bgraTight)
    {
        bgraTight = [];

        var width = bitmap.Width;
        var height = bitmap.Height;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        var rect = new Rectangle(0, 0, width, height);
        BitmapData? data = null;
        try
        {
            data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var stride = Math.Abs(data.Stride);
            var expectedTightRow = checked(width * 4);
            if (stride < expectedTightRow)
            {
                return false;
            }

            bgraTight = new byte[checked(expectedTightRow * height)];
            for (var y = 0; y < height; y++)
            {
                var srcRowPtr = IntPtr.Add(data.Scan0, y * stride);
                Marshal.Copy(srcRowPtr, bgraTight, y * expectedTightRow, expectedTightRow);
            }

            return true;
        }
        catch
        {
            bgraTight = [];
            return false;
        }
        finally
        {
            try
            {
                if (data is not null)
                {
                    bitmap.UnlockBits(data);
                }
            }
            catch
            {
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(nint hwnd, nint hdcBlt, uint nFlags);
}

