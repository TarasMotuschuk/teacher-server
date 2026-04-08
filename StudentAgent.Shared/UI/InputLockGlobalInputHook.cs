using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StudentAgent.UI;

/// <summary>
/// Session-wide low-level keyboard/mouse hooks while input lock overlays are visible.
/// Form-level handlers are not enough: the Windows key, Alt+Tab, etc. never reach a WinForms window reliably.
/// </summary>
/// <remarks>
/// Ctrl+Alt+Del (secure attention sequence) is handled below user-mode hooks and may still appear.
/// </remarks>
internal static class InputLockGlobalInputHook
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;

    private static readonly object Sync = new();
    private static int _refCount;
    private static IntPtr _keyboardHook;
    private static IntPtr _mouseHook;
    private static HookProc? _keyboardProc;
    private static HookProc? _mouseProc;

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    public static void AddRef()
    {
        lock (Sync)
        {
            checked
            {
                _refCount++;
            }

            if (_refCount != 1)
            {
                return;
            }

            if (!TryInstall())
            {
                _refCount = 0;
            }
        }
    }

    public static void Release()
    {
        lock (Sync)
        {
            if (_refCount <= 0)
            {
                _refCount = 0;
                return;
            }

            _refCount--;
            if (_refCount > 0)
            {
                return;
            }

            Uninstall();
        }
    }

    private static bool TryInstall()
    {
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;

        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, IntPtr.Zero, 0);
        if (_keyboardHook == IntPtr.Zero)
        {
            Debug.WriteLine($"Input lock: SetWindowsHookEx(KEYBOARD_LL) failed: {Marshal.GetLastWin32Error()}");
            _keyboardProc = null;
            _mouseProc = null;
            return false;
        }

        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, IntPtr.Zero, 0);
        if (_mouseHook == IntPtr.Zero)
        {
            Debug.WriteLine($"Input lock: SetWindowsHookEx(MOUSE_LL) failed: {Marshal.GetLastWin32Error()}");
            _ = UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
            _keyboardProc = null;
            _mouseProc = null;
            return false;
        }

        return true;
    }

    private static void Uninstall()
    {
        if (_mouseHook != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        _mouseProc = null;
        _keyboardProc = null;
    }

    private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode < 0)
            {
                return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            }

            // Swallow all keyboard input (including Left/Right Win, Alt+Tab, etc.).
            return (IntPtr)1;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Input lock keyboard hook failed: {ex}");
            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }
    }

    private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode < 0)
            {
                return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
            }

            return (IntPtr)1;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Input lock mouse hook failed: {ex}");
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
}
