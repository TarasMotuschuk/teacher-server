using RemoteViewing.Vnc;
using System.Runtime.InteropServices;

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private void HandleTouchEventCore(PointerChangedEventArgs e)
    {
        var virtualLeft = GetSystemMetrics(76);
        var virtualTop = GetSystemMetrics(77);
        _ = SetCursorPos(virtualLeft + e.X, virtualTop + e.Y);

        var currentButtons = e.PressedButtons & (ButtonLeft | ButtonMiddle | ButtonRight);
        SendButtonChanges(_pressedButtons, currentButtons);
        _pressedButtons = currentButtons;

        if ((e.PressedButtons & ButtonWheelUp) != 0)
        {
            SendMouseInput(MouseeventfWheel, WheelDelta);
        }

        if ((e.PressedButtons & ButtonWheelDown) != 0)
        {
            SendMouseInput(MouseeventfWheel, unchecked((uint)-WheelDelta));
        }
    }

    private void SendButtonChanges(int previousButtons, int currentButtons)
    {
        SendButtonChange(previousButtons, currentButtons, ButtonLeft, MouseeventfLeftdown, MouseeventfLeftup);
        SendButtonChange(previousButtons, currentButtons, ButtonMiddle, MouseeventfMiddledown, MouseeventfMiddleup);
        SendButtonChange(previousButtons, currentButtons, ButtonRight, MouseeventfRightdown, MouseeventfRightup);
    }

    private void SendButtonChange(int previousButtons, int currentButtons, int mask, uint downFlag, uint upFlag)
    {
        var wasPressed = (previousButtons & mask) != 0;
        var isPressed = (currentButtons & mask) != 0;
        if (wasPressed == isPressed)
        {
            return;
        }

        SendMouseInput(isPressed ? downFlag : upFlag, 0);
    }

    private static void SendMouseInput(uint flags, uint mouseData)
    {
        var input = new INPUT
        {
            Type = InputMouse,
            U = new InputUnion
            {
                Mi = new MOUSEINPUT
                {
                    Dx = 0,
                    Dy = 0,
                    MouseData = mouseData,
                    DwFlags = flags | ((flags & MouseeventfWheel) == 0 ? MouseeventfMove : 0),
                    DwExtraInfo = UIntPtr.Zero,
                },
            },
        };

        var inputs = new[] { input };
        _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

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
}
