using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using SIPSorceryMedia.Abstractions;

namespace TeacherClient.CrossPlatform.Services;

public sealed class WindowsScreenCaptureProducer : IDisposable
{
    private readonly object _sync = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public void Start(Rectangle captureArea, int captureFps, Action<uint, int, int, byte[], VideoPixelFormatsEnum> onFrame)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("WindowsScreenCaptureProducer can only run on Windows.");
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

        var width = Math.Max(1, captureArea.Width);
        var height = Math.Max(1, captureArea.Height);

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);

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
                g.CopyFromScreen(captureArea.X, captureArea.Y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
                if (TryGetBgraTight(bitmap, out var bgra))
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

    private static bool TryGetBgraTight(Bitmap bitmap, out byte[] bgraTight)
    {
        bgraTight = Array.Empty<byte>();

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
            bgraTight = Array.Empty<byte>();
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
}
