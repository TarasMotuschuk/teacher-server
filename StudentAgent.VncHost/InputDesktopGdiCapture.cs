using System.Drawing;
using System.Runtime.InteropServices;

namespace StudentAgent.VncHost;

/// <summary>
/// GDI screen capture on the current <see cref="OpenInputDesktop"/> so Winlogon / UAC secure UI is visible.
/// <see cref="Graphics.CopyFromScreen"/> without switching desktops reads the thread's default desktop, which is
/// not the login or elevation surface. Uses a dedicated thread with no HWNDs so <see cref="SetThreadDesktop"/> succeeds.
/// </summary>
internal static class InputDesktopGdiCapture
{
    private static readonly AutoResetEvent NeedWork = new(false);
    private static readonly AutoResetEvent Done = new(false);
    private static readonly object DispatchLock = new();
    private static Action? _work;
    private static Exception? _error;
    private static Thread? _worker;

    /// <summary>
    /// Copies the virtual-screen rectangle into <paramref name="bitmap"/> (32bpp RGB).
    /// </summary>
    public static void CopyVirtualScreenToBitmap(Rectangle bounds, int width, int height, Bitmap bitmap)
    {
        Run(() =>
        {
            if (!TryCopyWithInputDesktop(bounds, width, height, bitmap))
            {
                using var graphics = Graphics.FromImage(bitmap);
                graphics.CopyFromScreen(
                    bounds.Left,
                    bounds.Top,
                    0,
                    0,
                    new Size(width, height),
                    CopyPixelOperation.SourceCopy);
            }
        });
    }

    private static void Run(Action work)
    {
        EnsureWorker();
        lock (DispatchLock)
        {
            _error = null;
            _work = work;
            NeedWork.Set();
            Done.WaitOne();
            if (_error is not null)
            {
                throw new InvalidOperationException("VNC GDI capture worker failed.", _error);
            }
        }
    }

    private static void EnsureWorker()
    {
        if (_worker is not null)
        {
            return;
        }

        lock (DispatchLock)
        {
            if (_worker is not null)
            {
                return;
            }

            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "VncInputDesktopGdiCapture"
            };
            _worker.Start();
        }
    }

    private static void WorkerLoop()
    {
        while (true)
        {
            NeedWork.WaitOne();
            try
            {
                _work!();
            }
            catch (Exception ex)
            {
                _error = ex;
            }
            finally
            {
                Done.Set();
            }
        }
    }

    private static bool TryCopyWithInputDesktop(Rectangle bounds, int width, int height, Bitmap bitmap)
    {
        var hInput = TryOpenInputDesktop();
        if (hInput == IntPtr.Zero)
        {
            return false;
        }

        var threadId = GetCurrentThreadId();
        var hOld = GetThreadDesktop(threadId);
        if (!SetThreadDesktop(hInput))
        {
            _ = CloseDesktop(hInput);
            return false;
        }

        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(
                bounds.Left,
                bounds.Top,
                0,
                0,
                new Size(width, height),
                CopyPixelOperation.SourceCopy);
            return true;
        }
        finally
        {
            _ = SetThreadDesktop(hOld);
            _ = CloseDesktop(hInput);
        }
    }

    private static IntPtr TryOpenInputDesktop()
    {
        const uint genericAll = 0x10000000;
        var h = OpenInputDesktop(0, false, genericAll);
        if (h != IntPtr.Zero)
        {
            return h;
        }

        const uint access = 0x0001u | 0x0040u | 0x0100u | 0x0080u;
        return OpenInputDesktop(0, false, access);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseDesktop(IntPtr hDesktop);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetThreadDesktop(IntPtr hDesktop);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetThreadDesktop(uint dwThreadId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
