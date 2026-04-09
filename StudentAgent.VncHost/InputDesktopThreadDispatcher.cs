using System.Runtime.InteropServices;

namespace StudentAgent.VncHost;

internal static class InputDesktopThreadDispatcher
{
    private const int UoiName = 2;
    private static readonly AutoResetEvent NeedWork = new(false);
    private static readonly AutoResetEvent Done = new(false);
    private static readonly object DispatchLock = new();
    private static Action? _work;
    private static Exception? _error;
    private static Thread? _worker;

    public static void Run(Action work)
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
                throw new InvalidOperationException("Input desktop worker failed.", _error);
            }
        }
    }

    public static bool IsDefaultInputDesktop()
    {
        var inputDesktop = TryOpenInputDesktop();
        if (inputDesktop == IntPtr.Zero)
        {
            return true;
        }

        try
        {
            var name = GetDesktopName(inputDesktop);
            return string.IsNullOrEmpty(name) || string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            _ = CloseDesktop(inputDesktop);
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
                Name = "VncInputDesktopDispatcher",
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
                RunOnInputDesktop(_work!);
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

    private static void RunOnInputDesktop(Action work)
    {
        var inputDesktop = TryOpenInputDesktop();
        if (inputDesktop == IntPtr.Zero)
        {
            work();
            return;
        }

        var threadId = GetCurrentThreadId();
        var previousDesktop = GetThreadDesktop(threadId);
        if (!SetThreadDesktop(inputDesktop))
        {
            _ = CloseDesktop(inputDesktop);
            work();
            return;
        }

        try
        {
            work();
        }
        finally
        {
            _ = SetThreadDesktop(previousDesktop);
            _ = CloseDesktop(inputDesktop);
        }
    }

    private static IntPtr TryOpenInputDesktop()
    {
        const uint genericAll = 0x10000000;
        var desktop = OpenInputDesktop(0, false, genericAll);
        if (desktop != IntPtr.Zero)
        {
            return desktop;
        }

        const uint access = 0x0001u | 0x0040u | 0x0080u | 0x0100u;
        return OpenInputDesktop(0, false, access);
    }

    private static string? GetDesktopName(IntPtr desktop)
    {
        _ = GetUserObjectInformation(desktop, UoiName, IntPtr.Zero, 0, out var needed);
        if (needed <= 0)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal(needed);
        try
        {
            if (!GetUserObjectInformation(desktop, UoiName, buffer, needed, out _))
            {
                return null;
            }

            return Marshal.PtrToStringUni(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseDesktop(IntPtr hDesktop);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetUserObjectInformation(
        IntPtr hObj,
        int nIndex,
        IntPtr pvInfo,
        int nLength,
        out int lpnLengthNeeded);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetThreadDesktop(IntPtr hDesktop);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetThreadDesktop(uint dwThreadId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
