using System.Runtime.InteropServices;
using RemoteViewing.Vnc;

namespace StudentAgent.VncHost;

internal sealed class WindowsVncRemoteController : IVncRemoteController
{
    private const int ButtonLeft = 1;
    private const int ButtonMiddle = 2;
    private const int ButtonRight = 4;
    private const int ButtonWheelUp = 8;
    private const int ButtonWheelDown = 16;

    private const uint InputMouse = 0;
    private const uint MouseeventfMove = 0x0001;
    private const uint MouseeventfLeftdown = 0x0002;
    private const uint MouseeventfLeftup = 0x0004;
    private const uint MouseeventfRightdown = 0x0008;
    private const uint MouseeventfRightup = 0x0010;
    private const uint MouseeventfMiddledown = 0x0020;
    private const uint MouseeventfMiddleup = 0x0040;
    private const uint MouseeventfWheel = 0x0800;
    private const int WheelDelta = 120;

    private readonly object _sync = new();
    private int _pressedButtons;

    public void HandleTouchEvent(object? sender, PointerChangedEventArgs e)
    {
        lock (_sync)
        {
            InputDesktopThreadDispatcher.Run(() => HandleTouchEventCore(e));
        }
    }

    private static void SendButtonChanges(int previousButtons, int currentButtons)
    {
        SendButtonChange(previousButtons, currentButtons, ButtonLeft, MouseeventfLeftdown, MouseeventfLeftup);
        SendButtonChange(previousButtons, currentButtons, ButtonMiddle, MouseeventfMiddledown, MouseeventfMiddleup);
        SendButtonChange(previousButtons, currentButtons, ButtonRight, MouseeventfRightdown, MouseeventfRightup);
    }

    private static void SendButtonChange(int previousButtons, int currentButtons, int mask, uint downFlag, uint upFlag)
    {
        var wasPressed = (previousButtons & mask) != 0;
        var isPressed = (currentButtons & mask) != 0;
        if (wasPressed == isPressed)
        {
            return;
        }

        NativeMethods.SendMouseInput(isPressed ? downFlag : upFlag, 0, InputMouse, MouseeventfMove, MouseeventfWheel);
    }

    private void HandleTouchEventCore(PointerChangedEventArgs e)
    {
        var virtualLeft = NativeMethods.GetSystemMetrics(76);
        var virtualTop = NativeMethods.GetSystemMetrics(77);
        _ = NativeMethods.SetCursorPos(virtualLeft + e.X, virtualTop + e.Y);

        var currentButtons = e.PressedButtons & (ButtonLeft | ButtonMiddle | ButtonRight);
        SendButtonChanges(_pressedButtons, currentButtons);
        _pressedButtons = currentButtons;

        if ((e.PressedButtons & ButtonWheelUp) != 0)
        {
            NativeMethods.SendMouseInput(MouseeventfWheel, WheelDelta, InputMouse, MouseeventfMove, MouseeventfWheel);
        }

        if ((e.PressedButtons & ButtonWheelDown) != 0)
        {
            NativeMethods.SendMouseInput(MouseeventfWheel, unchecked((uint)-WheelDelta), InputMouse, MouseeventfMove, MouseeventfWheel);
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        internal static extern int GetSystemMetrics(int nIndex);

        internal static void SendMouseInput(uint flags, uint mouseData, uint inputMouse, uint mouseeventfMove, uint mouseeventfWheel)
        {
            var input = new INPUT
            {
                Type = inputMouse,
                U = new InputUnion
                {
                    Mi = new MOUSEINPUT
                    {
                        Dx = 0,
                        Dy = 0,
                        MouseData = mouseData,
                        DwFlags = flags | ((flags & mouseeventfWheel) == 0 ? mouseeventfMove : 0),
                        DwExtraInfo = UIntPtr.Zero,
                    },
                },
            };

            var inputs = new[] { input };
            _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct INPUT
        {
            public uint Type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT Mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MOUSEINPUT
        {
            public int Dx;
            public int Dy;
            public uint MouseData;
            public uint DwFlags;
            public uint Time;
            public UIntPtr DwExtraInfo;
        }
    }
}
