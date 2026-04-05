using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Teacher.Common.Vnc;
using TeacherClient.CrossPlatform.Localization;

namespace TeacherClient.CrossPlatform.Dialogs;

public partial class RemoteVncViewerWindow : Window
{
    private readonly TeacherVncSession _session;
    private readonly DispatcherTimer _refreshTimer = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly string _machineName;
    private PinnedBitmap? _currentBitmap;
    private int _frameWidth;
    private int _frameHeight;

    public RemoteVncViewerWindow(string machineName, string host, int port, string sharedSecret, bool controlEnabled)
    {
        _machineName = machineName;
        _session = new TeacherVncSession(host, port, sharedSecret, controlEnabled);

        InitializeComponent();
        Icon = AppIconLoader.Load();
        Title = CrossPlatformText.RemoteManagementViewerTitle(machineName);
        WindowState = WindowState.Maximized;

        _refreshTimer.Interval = TimeSpan.FromMilliseconds(500);
        _refreshTimer.Tick += async (_, _) => await CaptureFrameAsync();

        Opened += async (_, _) =>
        {
            await ConnectAsync();
            _refreshTimer.Start();
        };

        Closing += (_, _) =>
        {
            _refreshTimer.Stop();
            _cancellation.Cancel();
            _session.Dispose();
            _cancellation.Dispose();
            DisposeBitmap();
        };

        ScreenImage.PointerPressed += ScreenImage_OnPointerPressed;
        ScreenImage.PointerReleased += ScreenImage_OnPointerReleased;
        ScreenImage.PointerMoved += ScreenImage_OnPointerMoved;
        ScreenImage.PointerWheelChanged += ScreenImage_OnPointerWheelChanged;
        KeyDown += RemoteVncViewerWindow_KeyDown;
        KeyUp += RemoteVncViewerWindow_KeyUp;
        TextInput += RemoteVncViewerWindow_TextInput;

        StatusTextBlock.Text = controlEnabled
            ? CrossPlatformText.RemoteManagementControl(machineName)
            : CrossPlatformText.RemoteManagementViewOnly(machineName);
    }

    private async Task ConnectAsync()
    {
        try
        {
            StatusTextBlock.Text = CrossPlatformText.RemoteManagementConnecting(_machineName);
            await _session.ConnectAsync(_cancellation.Token);
            StatusTextBlock.Text = _session.ControlEnabled
                ? CrossPlatformText.RemoteManagementControl(_machineName)
                : CrossPlatformText.RemoteManagementViewOnly(_machineName);
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = CrossPlatformText.RemoteManagementConnectionFailed(_machineName, ex.Message);
        }
    }

    private async Task CaptureFrameAsync()
    {
        if (_cancellation.IsCancellationRequested || !_session.IsConnected)
        {
            return;
        }

        try
        {
            var frame = await _session.CaptureFrameAsync(_cancellation.Token);
            if (frame is null)
            {
                return;
            }

            var bitmap = CreatePinnedBitmap(frame);
            if (Dispatcher.UIThread.CheckAccess())
            {
                SetBitmap(bitmap);
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() => SetBitmap(bitmap));
            }

            _frameWidth = frame.Width;
            _frameHeight = frame.Height;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = CrossPlatformText.RemoteManagementConnectionFailed(_machineName, ex.Message);
        }
    }

    private void SetBitmap(Bitmap bitmap)
    {
        DisposeBitmap();
        ScreenImage.Source = bitmap;
        _frameWidth = bitmap.PixelSize.Width;
        _frameHeight = bitmap.PixelSize.Height;
    }

    private void DisposeBitmap()
    {
        ScreenImage.Source = null;
        _currentBitmap?.Dispose();
        _currentBitmap = null;
    }

    private void ScreenImage_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus();
        if (!_session.ControlEnabled || !_session.IsConnected || _frameWidth <= 0 || _frameHeight <= 0)
        {
            return;
        }

        if (TryMapPointer(e.GetPosition(ScreenImage), out var x, out var y))
        {
            _session.SendPointer(x, y, ButtonsMask(e.GetCurrentPoint(ScreenImage).Properties));
        }
    }

    private void ScreenImage_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_session.ControlEnabled || !_session.IsConnected || _frameWidth <= 0 || _frameHeight <= 0)
        {
            return;
        }

        if (TryMapPointer(e.GetPosition(ScreenImage), out var x, out var y))
        {
            _session.SendPointer(x, y, 0);
        }
    }

    private void ScreenImage_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_session.ControlEnabled || !_session.IsConnected || _frameWidth <= 0 || _frameHeight <= 0)
        {
            return;
        }

        if (TryMapPointer(e.GetPosition(ScreenImage), out var x, out var y))
        {
            var properties = e.GetCurrentPoint(ScreenImage).Properties;
            var pressed = ButtonsMask(properties);
            _session.SendPointer(x, y, pressed);
        }
    }

    private void ScreenImage_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (!_session.ControlEnabled || !_session.IsConnected || _frameWidth <= 0 || _frameHeight <= 0)
        {
            return;
        }

        if (TryMapPointer(e.GetPosition(ScreenImage), out var x, out var y))
        {
            var buttons = e.Delta.Y > 0 ? 8 : 16;
            _session.SendPointer(x, y, buttons);
            _session.SendPointer(x, y, 0);
        }
    }

    private void RemoteVncViewerWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!_session.ControlEnabled)
        {
            return;
        }

        var keySym = MapSpecialKey(e.Key);
        if (keySym is not null)
        {
            _session.SendKey(keySym.Value, true);
            e.Handled = true;
        }
    }

    private void RemoteVncViewerWindow_KeyUp(object? sender, KeyEventArgs e)
    {
        if (!_session.ControlEnabled)
        {
            return;
        }

        var keySym = MapSpecialKey(e.Key);
        if (keySym is not null)
        {
            _session.SendKey(keySym.Value, false);
            e.Handled = true;
        }
    }

    private void RemoteVncViewerWindow_TextInput(object? sender, TextInputEventArgs e)
    {
        if (!_session.ControlEnabled || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        foreach (var character in e.Text)
        {
            if (char.IsControl(character))
            {
                continue;
            }

            var keySym = RemoteViewing.Vnc.KeySymHelpers.FromChar(character);
            _session.SendKey(keySym, true);
            _session.SendKey(keySym, false);
        }
    }

    private bool TryMapPointer(Point position, out int x, out int y)
    {
        var bounds = ScreenImage.Bounds;
        var width = Math.Max(1, bounds.Width);
        var height = Math.Max(1, bounds.Height);

        var scale = Math.Min(width / _frameWidth, height / _frameHeight);
        var displayedWidth = _frameWidth * scale;
        var displayedHeight = _frameHeight * scale;
        var offsetX = (width - displayedWidth) / 2;
        var offsetY = (height - displayedHeight) / 2;

        if (position.X < offsetX || position.Y < offsetY ||
            position.X > offsetX + displayedWidth || position.Y > offsetY + displayedHeight)
        {
            x = 0;
            y = 0;
            return false;
        }

        x = Math.Clamp((int)Math.Round((position.X - offsetX) / scale), 0, _frameWidth - 1);
        y = Math.Clamp((int)Math.Round((position.Y - offsetY) / scale), 0, _frameHeight - 1);
        return true;
    }

    private static int ButtonsMask(PointerPointProperties properties)
    {
        var mask = 0;
        if (properties.IsLeftButtonPressed)
        {
            mask |= 1;
        }
        if (properties.IsMiddleButtonPressed)
        {
            mask |= 2;
        }
        if (properties.IsRightButtonPressed)
        {
            mask |= 4;
        }

        return mask;
    }

    private static RemoteViewing.Vnc.KeySym? MapSpecialKey(Key key)
    {
        return key switch
        {
            Key.Back => RemoteViewing.Vnc.KeySym.Backspace,
            Key.Tab => RemoteViewing.Vnc.KeySym.Tab,
            Key.Enter => RemoteViewing.Vnc.KeySym.Return,
            Key.Escape => RemoteViewing.Vnc.KeySym.Escape,
            Key.Space => RemoteViewing.Vnc.KeySym.Space,
            Key.PageUp => RemoteViewing.Vnc.KeySym.PageUp,
            Key.PageDown => RemoteViewing.Vnc.KeySym.PageDown,
            Key.End => RemoteViewing.Vnc.KeySym.End,
            Key.Home => RemoteViewing.Vnc.KeySym.Home,
            Key.Left => RemoteViewing.Vnc.KeySym.Left,
            Key.Up => RemoteViewing.Vnc.KeySym.Up,
            Key.Right => RemoteViewing.Vnc.KeySym.Right,
            Key.Down => RemoteViewing.Vnc.KeySym.Down,
            Key.Insert => RemoteViewing.Vnc.KeySym.Insert,
            Key.Delete => RemoteViewing.Vnc.KeySym.Delete,
            Key.F1 => RemoteViewing.Vnc.KeySym.F1,
            Key.F2 => RemoteViewing.Vnc.KeySym.F2,
            Key.F3 => RemoteViewing.Vnc.KeySym.F3,
            Key.F4 => RemoteViewing.Vnc.KeySym.F4,
            Key.F5 => RemoteViewing.Vnc.KeySym.F5,
            Key.F6 => RemoteViewing.Vnc.KeySym.F6,
            Key.F7 => RemoteViewing.Vnc.KeySym.F7,
            Key.F8 => RemoteViewing.Vnc.KeySym.F8,
            Key.F9 => RemoteViewing.Vnc.KeySym.F9,
            Key.F10 => RemoteViewing.Vnc.KeySym.F10,
            Key.F11 => RemoteViewing.Vnc.KeySym.F11,
            Key.F12 => RemoteViewing.Vnc.KeySym.F12,
            Key.LeftShift => RemoteViewing.Vnc.KeySym.ShiftLeft,
            Key.RightShift => RemoteViewing.Vnc.KeySym.ShiftRight,
            Key.LeftCtrl => RemoteViewing.Vnc.KeySym.ControlLeft,
            Key.RightCtrl => RemoteViewing.Vnc.KeySym.ControlRight,
            Key.LeftAlt => RemoteViewing.Vnc.KeySym.AltLeft,
            Key.RightAlt => RemoteViewing.Vnc.KeySym.AltRight,
            _ => null
        };
    }

    private void SetBitmap(PinnedBitmap bitmap)
    {
        DisposeBitmap();
        _currentBitmap = bitmap;
        ScreenImage.Source = bitmap.Bitmap;
        _frameWidth = bitmap.Bitmap.PixelSize.Width;
        _frameHeight = bitmap.Bitmap.PixelSize.Height;
    }

    private static PinnedBitmap CreatePinnedBitmap(VncFrameCapture frame)
    {
        var pixels = frame.Pixels;
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            var bitmap = new Bitmap(
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul,
                handle.AddrOfPinnedObject(),
                new PixelSize(frame.Width, frame.Height),
                new Vector(96, 96),
                frame.Stride);
            return new PinnedBitmap(bitmap, handle);
        }
        catch
        {
            handle.Free();
            throw;
        }
    }

    private sealed class PinnedBitmap(Bitmap bitmap, GCHandle handle) : IDisposable
    {
        public Bitmap Bitmap { get; } = bitmap;
        private GCHandle Handle { get; } = handle;

        public void Dispose()
        {
            Bitmap.Dispose();
            if (Handle.IsAllocated)
            {
                Handle.Free();
            }
        }
    }
}
