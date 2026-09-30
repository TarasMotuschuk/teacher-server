using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ClassCommander.TestRunner.Services;

/// <summary>Blocks common task-switch shortcuts only while a visible test is in progress.</summary>
internal sealed class TestSessionKeyboardGuard : IDisposable
{
    private readonly HookCallback _callback;
    private IntPtr _hook;

    public TestSessionKeyboardGuard()
    {
        _callback = FilterKey;
        if (OperatingSystem.IsWindows())
        {
            _hook = SetWindowsHookEx(13, _callback, IntPtr.Zero, 0);
            if (_hook == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
    }

    private delegate IntPtr HookCallback(int code, IntPtr message, IntPtr data);

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        GC.KeepAlive(_callback);
    }

    private IntPtr FilterKey(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0)
        {
            var key = Marshal.ReadInt32(data);
            var alt = (Marshal.ReadInt32(data, 8) & 0x20) != 0;
            var control = (GetAsyncKeyState(0x11) & 0x8000) != 0;

            // Leave Ctrl+Alt+Del and OS recovery available. Alt+F4 reaches the PIN dialog.
            if (key is 0x5B or 0x5C || (alt && (key is 0x09 or 0x1B)) || (control && key == 0x1B))
            {
                return (IntPtr)1;
            }
        }

        return CallNextHookEx(_hook, code, message, data);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookCallback callback, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
}
