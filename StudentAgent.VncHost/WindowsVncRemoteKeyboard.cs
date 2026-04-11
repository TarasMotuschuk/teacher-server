using System.Diagnostics;
using System.Runtime.InteropServices;
using RemoteViewing.Vnc;
using StudentAgent.Services;
using KeySym = RemoteViewing.Vnc.KeySym;

namespace StudentAgent.VncHost;

internal sealed class WindowsVncRemoteKeyboard : IVncRemoteKeyboard
{
    private const uint InputKeyboard = 1;
    private const uint KeyeventfExtendedkey = 0x0001;
    private const uint KeyeventfKeyup = 0x0002;
    private const uint KeyeventfUnicode = 0x0004;

    private readonly AgentLogService _logService;
    private readonly object _sync = new();
    private int _sendInputFailureLogBudget = 8;
    private int _ctrlDownCount;
    private int _altDownCount;
    private bool _skipNextDeleteKeyUp;

    public WindowsVncRemoteKeyboard(AgentLogService logService)
    {
        _logService = logService;
    }

    public void HandleKeyEvent(object? sender, KeyChangedEventArgs e)
    {
        try
        {
            lock (_sync)
            {
                InputDesktopThreadDispatcher.Run(() => HandleKeyEventCore(e));
            }
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"VNC keyboard event failed: {ex.Message}");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private void HandleKeyEventCore(KeyChangedEventArgs e)
    {
        var keysymU = (uint)e.Keysym;
        UpdateModifierTally(keysymU, e.Pressed);

        // Windows blocks synthetic Ctrl+Alt+Del (SAS). Open Task Manager instead — usual classroom need.
        if (keysymU == 0xFFFF && e.Pressed && _ctrlDownCount > 0 && _altDownCount > 0 &&
            TryLaunchTaskManagerFromCadShortcut())
        {
            _skipNextDeleteKeyUp = true;
            return;
        }

        if (keysymU == 0xFFFF && !e.Pressed && _skipNextDeleteKeyUp)
        {
            _skipNextDeleteKeyUp = false;
            return;
        }

        if (TryMapVirtualKey(e.Keysym, out var virtualKey, out var scanCode, out var keyFlags))
        {
            SendVirtualKey(virtualKey, scanCode, e.Pressed, keyFlags, (uint)e.Keysym);
            return;
        }

        var keysymValue = (uint)e.Keysym;
        if (keysymValue is >= 0x20 and <= 0xFFFF)
        {
            SendUnicode((char)keysymValue, e.Pressed);
        }
    }

    private static bool TryMapVirtualKey(KeySym keySym, out ushort virtualKey, out ushort scanCode, out uint keyFlags)
    {
        virtualKey = 0;
        scanCode = 0;
        keyFlags = 0;

        switch ((uint)keySym)
        {
            case 0xFF08:
                virtualKey = 0x08;
                return true;
            case 0xFF09:
                virtualKey = 0x09;
                return true;
            case 0xFF0D:
                virtualKey = 0x0D;
                return true;
            case 0xFF1B:
                virtualKey = 0x1B;
                return true;
            case 0xFF50:
                virtualKey = 0x24;
                keyFlags = KeyeventfExtendedkey;
                return true;
            case 0xFF51:
                virtualKey = 0x25;
                keyFlags = KeyeventfExtendedkey;
                return true;
            case 0xFF52:
                virtualKey = 0x26;
                keyFlags = KeyeventfExtendedkey;
                return true;
            case 0xFF53:
                virtualKey = 0x27;
                keyFlags = KeyeventfExtendedkey;
                return true;
            case 0xFF54:
                virtualKey = 0x28;
                keyFlags = KeyeventfExtendedkey;
                return true;
            case 0xFF55:
                virtualKey = 0x21;
                keyFlags = KeyeventfExtendedkey;
                return true;
            case 0xFF56:
                virtualKey = 0x22;
                keyFlags = KeyeventfExtendedkey;
                return true;
            case 0xFF57:
                virtualKey = 0x23;
                keyFlags = KeyeventfExtendedkey;
                return true;
            case 0xFF63:
                virtualKey = 0x2D;
                keyFlags = KeyeventfExtendedkey;
                return true;
            case 0xFFFF:
                virtualKey = 0x2E;
                keyFlags = KeyeventfExtendedkey;
                return true;
            case 0xFFE1:
                virtualKey = 0xA0;
                return true;
            case 0xFFE2:
                virtualKey = 0xA1;
                return true;
            case 0xFFE3:
                virtualKey = 0xA2;
                return true;
            case 0xFFE4:
                virtualKey = 0xA3;
                keyFlags = KeyeventfExtendedkey;
                return true;
            case 0xFFE9:
                virtualKey = 0xA4;
                return true;
            case 0xFFEA:
                virtualKey = 0xA5;
                keyFlags = KeyeventfExtendedkey;
                return true;

            // Left/Right Win — X11 Super_* / Meta_* (MarcusW KeySymbol.Super_L etc.)
            case 0xFFE7:
            case 0xFFEB:
                virtualKey = 0x5B;
                return true;
            case 0xFFE8:
            case 0xFFEC:
                virtualKey = 0x5C;
                return true;
        }

        var raw = (uint)keySym;
        if (raw is >= 0xFFBE and <= 0xFFC9)
        {
            virtualKey = (ushort)(0x70 + (raw - 0xFFBE));
            return true;
        }

        // Printable keysyms (e.g. Latin letters 0x61) must fall through to HandleKeyEventCore's SendUnicode path.
        // Returning true here with virtualKey=0 incorrectly sent VK 0 and skipped Unicode injection.
        return false;
    }

    private bool TryLaunchTaskManagerFromCadShortcut()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "taskmgr.exe",
                UseShellExecute = true,
            });
            return true;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"VNC Ctrl+Alt+Del shortcut: could not start Task Manager: {ex.Message}");
            return false;
        }
    }

    private void UpdateModifierTally(uint keysymU, bool pressed)
    {
        var delta = pressed ? 1 : -1;
        if (keysymU is 0xFFE3 or 0xFFE4)
        {
            _ctrlDownCount = Math.Max(0, _ctrlDownCount + delta);
        }
        else if (keysymU is 0xFFE9 or 0xFFEA)
        {
            _altDownCount = Math.Max(0, _altDownCount + delta);
        }
    }

    private void SendVirtualKey(ushort virtualKey, ushort scanCode, bool pressed, uint keyFlags, uint keysymForLog)
    {
        var input = new INPUT
        {
            Type = InputKeyboard,
            U = new InputUnion
            {
                Ki = new KEYBDINPUT
                {
                    WVk = virtualKey,
                    WScan = scanCode,
                    DwFlags = keyFlags | (pressed ? 0u : KeyeventfKeyup),
                    DwExtraInfo = UIntPtr.Zero,
                },
            },
        };

        SendSingleInput(input, keysymForLog);
    }

    private void SendUnicode(char character, bool pressed)
    {
        var input = new INPUT
        {
            Type = InputKeyboard,
            U = new InputUnion
            {
                Ki = new KEYBDINPUT
                {
                    WVk = 0,
                    WScan = character,
                    DwFlags = KeyeventfUnicode | (pressed ? 0u : KeyeventfKeyup),
                    DwExtraInfo = UIntPtr.Zero,
                },
            },
        };

        SendSingleInput(input, character);
    }

    private void SendSingleInput(INPUT input, uint keysymForLog)
    {
        var inputs = new[] { input };
        var size = Marshal.SizeOf<INPUT>();
        var sent = SendInput((uint)inputs.Length, inputs, size);
        if (sent != 0)
        {
            return;
        }

        if (_sendInputFailureLogBudget-- <= 0)
        {
            return;
        }

        _logService.LogWarning(
            $"VNC SendInput failed (keysym=0x{keysymForLog:X}, cbSize={size}, err={Marshal.GetLastWin32Error()}).");
    }

    /// <summary>
    /// Must match Win32 INPUT: the union size is that of the largest member (MOUSEINPUT), or SendInput rejects keyboard events on x64.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT Mi;

        [FieldOffset(0)]
        public KEYBDINPUT Ki;

        [FieldOffset(0)]
        public HARDWAREINPUT Hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint DwFlags;
        public uint Time;
        public UIntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint UMsg;
        public ushort WParamL;
        public ushort WParamH;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort WVk;
        public ushort WScan;
        public uint DwFlags;
        public uint Time;
        public UIntPtr DwExtraInfo;
    }
}
