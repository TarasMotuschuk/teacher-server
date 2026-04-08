using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using MarcusW.VncClient;
using Teacher.Common.Vnc;
using TeacherClient.CrossPlatform.Localization;

namespace TeacherClient.CrossPlatform.Dialogs;

public partial class RemoteVncViewerWindow : Window, IDisposable
{
    private static readonly IReadOnlyList<KeyboardShortcutOption> ShortcutOptions =
    [
        new(CrossPlatformText.SendKeyboardShortcut),
        new("Ctrl+Alt+Del", KeySymbol.Control_L, KeySymbol.Alt_L, KeySymbol.Delete),
        new("Ctrl+Shift+Esc", KeySymbol.Control_L, KeySymbol.Shift_L, KeySymbol.Escape),
        new("Alt+Tab", KeySymbol.Alt_L, KeySymbol.Tab),
        new("Alt+F4", KeySymbol.Alt_L, KeySymbol.F4),
        new("Win", KeySymbol.Super_L),
        new("Win+Tab", KeySymbol.Super_L, KeySymbol.Tab),
        new("Win+R", KeySymbol.Super_L, KeySymbol.R),
        new("Win+D", KeySymbol.Super_L, KeySymbol.D),
        new("Ctrl+Esc", KeySymbol.Control_L, KeySymbol.Escape),
        new("Print Screen", KeySymbol.Print)
    ];

    private readonly TeacherVncSession _session;
    private readonly bool _ownsSession;
    private readonly DispatcherTimer _refreshTimer = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly string _machineName;
    private int _captureInProgress;
    private int _connectInProgress;
    private PinnedBitmap? _currentBitmap;
    private int _frameWidth;
    private int _frameHeight;
    private bool _updatingShortcutSelection;
    private int _pointerButtonsMask;
    private bool _disposed;

    public RemoteVncViewerWindow(string machineName, string host, int port, string sharedSecret, bool controlEnabled)
        : this(machineName, new TeacherVncSession(host, port, sharedSecret, controlEnabled), ownsSession: true)
    {
    }

    public RemoteVncViewerWindow(string machineName, TeacherVncSession session, bool ownsSession = false)
    {
        _machineName = machineName;
        _session = session;
        _ownsSession = ownsSession;

        InitializeComponent();
        RenderOptions.SetBitmapInterpolationMode(ScreenImage, BitmapInterpolationMode.HighQuality);
        Icon = AppIconLoader.Load();
        Title = CrossPlatformText.RemoteManagementViewerTitle(machineName);

        ConfigureWindowForCurrentPlatform();

        _refreshTimer.Interval = TimeSpan.FromMilliseconds(500);
        _refreshTimer.Tick += async (_, _) => await CaptureFrameAsync();

        Opened += async (_, _) =>
        {
            ApplyPreferredInitialLayout();
            await ConnectAsync();

            if (_cancellation.IsCancellationRequested || _disposed)
            {
                return;
            }

            _refreshTimer.Start();

            // Defer: focus before layout can fail on macOS; keyboard/TextInput attach to ViewerInputRoot, not the window chrome.
            Dispatcher.UIThread.Post(
                () => ViewerInputRoot.Focus(),
                DispatcherPriority.Loaded);
        };

        Closing += (_, _) =>
        {
            Dispose();
        };

        ScreenImage.PointerPressed += ScreenImage_OnPointerPressed;
        ScreenImage.PointerReleased += ScreenImage_OnPointerReleased;
        ScreenImage.PointerMoved += ScreenImage_OnPointerMoved;
        ScreenImage.PointerWheelChanged += ScreenImage_OnPointerWheelChanged;
        ViewerInputRoot.KeyDown += ViewerInputRoot_OnKeyDown;
        ViewerInputRoot.KeyUp += ViewerInputRoot_OnKeyUp;
        ViewerInputRoot.TextInput += ViewerInputRoot_OnTextInput;
        SendShortcutComboBox.ItemsSource = ShortcutOptions;
        SendShortcutComboBox.SelectedIndex = 0;
        EnableControlButton.Content = CrossPlatformText.EnableFullscreenControl;
        UpdateControlUi();
    }

    private void ConfigureWindowForCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            SystemDecorations = SystemDecorations.None;
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
            return;
        }

        // CenterOwner in XAML conflicts with maximize transitions on some builds; apply the
        // preferred state after the window is opened.
        WindowStartupLocation = WindowStartupLocation.Manual;
    }

    private void ApplyPreferredInitialLayout() => WindowState = WindowState.Maximized;

    private async Task ConnectAsync()
    {
        Interlocked.Increment(ref _connectInProgress);
        try
        {
            StatusTextBlock.Text = CrossPlatformText.RemoteManagementConnecting(_machineName);
            if (!_session.IsConnected)
            {
                await Task.Run(async () => await _session.ConnectAsync(_cancellation.Token));
            }

            if (_disposed || _cancellation.IsCancellationRequested)
            {
                return;
            }

            UpdateControlUi();
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException and not ObjectDisposedException && !_disposed)
            {
                StatusTextBlock.Text = CrossPlatformText.RemoteManagementConnectionFailed(_machineName, ex.Message);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _connectInProgress);
        }
    }

    private async Task CaptureFrameAsync()
    {
        if (_cancellation.IsCancellationRequested || !_session.IsConnected)
        {
            return;
        }

        if (Interlocked.Exchange(ref _captureInProgress, 1) != 0)
        {
            return;
        }

        try
        {
            var capture = await Task.Run(
                async () =>
                {
                    var frame = await _session.CaptureFrameAsync(_cancellation.Token);
                    if (frame is null)
                    {
                        return (Frame: (VncFrameCapture?)null, FrameWidth: 0, FrameHeight: 0);
                    }

                    var resized = ResizeForViewer(frame, VncViewerDisplayLimits.MaxFrameWidth, VncViewerDisplayLimits.MaxFrameHeight);
                    return (Frame: resized, FrameWidth: frame.Width, FrameHeight: frame.Height);
                },
                _cancellation.Token);

            if (capture.Frame is null || _disposed)
            {
                return;
            }

            _frameWidth = capture.FrameWidth;
            _frameHeight = capture.FrameHeight;

            var bitmap = CreatePinnedBitmap(capture.Frame);
            SetBitmap(bitmap);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            if (!_disposed)
            {
                StatusTextBlock.Text = CrossPlatformText.RemoteManagementConnectionFailed(_machineName, ex.Message);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _captureInProgress, 0);
        }
    }

    private void DisposeBitmap()
    {
        ScreenImage.Source = null;
        _currentBitmap?.Dispose();
        _currentBitmap = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _refreshTimer.Stop();
        _cancellation.Cancel();

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while ((Volatile.Read(ref _captureInProgress) != 0 || Volatile.Read(ref _connectInProgress) != 0) &&
               DateTime.UtcNow < deadline)
        {
            Thread.Sleep(20);
        }

        if (_ownsSession)
        {
            _session.Dispose();
        }

        _cancellation.Dispose();
        DisposeBitmap();
        GC.SuppressFinalize(this);
    }

    private void EnableControlButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _session.ControlEnabled = true;
        UpdateControlUi();
        ViewerInputRoot.Focus();
    }

    private async void SendShortcutComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingShortcutSelection || SendShortcutComboBox.SelectedItem is not KeyboardShortcutOption option || option.Keys.Count == 0)
        {
            return;
        }

        _session.ControlEnabled = true;
        UpdateControlUi();

        try
        {
            await _session.SendKeyCombinationAsync(option.Keys.ToArray());
        }
        finally
        {
            _updatingShortcutSelection = true;
            SendShortcutComboBox.SelectedIndex = 0;
            _updatingShortcutSelection = false;
            ViewerInputRoot.Focus();
        }
    }

    private void UpdateControlUi()
    {
        StatusTextBlock.Text = _session.ControlEnabled
            ? CrossPlatformText.RemoteManagementControl(_machineName)
            : CrossPlatformText.RemoteManagementViewOnly(_machineName);
        EnableControlButton.IsVisible = !_session.ControlEnabled;
    }

    private void ScreenImage_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ViewerInputRoot.Focus();
        if (!_session.ControlEnabled || !_session.IsConnected || _frameWidth <= 0 || _frameHeight <= 0)
        {
            return;
        }

        e.Pointer.Capture(ScreenImage);
        _pointerButtonsMask = UpdateButtonsMask(_pointerButtonsMask, e.GetCurrentPoint(ScreenImage).Properties);

        if (TryMapPointer(e.GetPosition(ScreenImage), out var x, out var y))
        {
            _session.SendPointer(x, y, _pointerButtonsMask);
        }
    }

    private void ScreenImage_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_session.ControlEnabled || !_session.IsConnected || _frameWidth <= 0 || _frameHeight <= 0)
        {
            return;
        }

        _pointerButtonsMask = UpdateButtonsMask(_pointerButtonsMask, e.GetCurrentPoint(ScreenImage).Properties);

        if (TryMapPointer(e.GetPosition(ScreenImage), out var x, out var y))
        {
            _session.SendPointer(x, y, 0);
        }

        e.Pointer.Capture(null);
        _pointerButtonsMask = 0;
    }

    private void ScreenImage_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_session.ControlEnabled || !_session.IsConnected || _frameWidth <= 0 || _frameHeight <= 0)
        {
            return;
        }

        _pointerButtonsMask = UpdateButtonsMask(_pointerButtonsMask, e.GetCurrentPoint(ScreenImage).Properties);

        if (TryMapPointer(e.GetPosition(ScreenImage), out var x, out var y))
        {
            _session.SendPointer(x, y, _pointerButtonsMask);
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

    private void ViewerInputRoot_OnKeyDown(object? sender, KeyEventArgs e)
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

    private void ViewerInputRoot_OnKeyUp(object? sender, KeyEventArgs e)
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

    private void ViewerInputRoot_OnTextInput(object? sender, TextInputEventArgs e)
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

            var keySym = (KeySymbol)(uint)character;
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

    private static int UpdateButtonsMask(int currentMask, PointerPointProperties properties)
    {
        switch (properties.PointerUpdateKind)
        {
            case PointerUpdateKind.LeftButtonPressed:
                return currentMask | 1;
            case PointerUpdateKind.LeftButtonReleased:
                return currentMask & ~1;
            case PointerUpdateKind.MiddleButtonPressed:
                return currentMask | 2;
            case PointerUpdateKind.MiddleButtonReleased:
                return currentMask & ~2;
            case PointerUpdateKind.RightButtonPressed:
                return currentMask | 4;
            case PointerUpdateKind.RightButtonReleased:
                return currentMask & ~4;
        }

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

    private static KeySymbol? MapSpecialKey(Key key)
    {
        return key switch
        {
            Key.Back => KeySymbol.BackSpace,
            Key.Tab => KeySymbol.Tab,
            Key.Enter => KeySymbol.Return,
            Key.Escape => KeySymbol.Escape,
            Key.PageUp => KeySymbol.Page_Up,
            Key.PageDown => KeySymbol.Page_Down,
            Key.End => KeySymbol.End,
            Key.Home => KeySymbol.Home,
            Key.Left => KeySymbol.Left,
            Key.Up => KeySymbol.Up,
            Key.Right => KeySymbol.Right,
            Key.Down => KeySymbol.Down,
            Key.Insert => KeySymbol.Insert,
            Key.Delete => KeySymbol.Delete,
            Key.F1 => KeySymbol.F1,
            Key.F2 => KeySymbol.F2,
            Key.F3 => KeySymbol.F3,
            Key.F4 => KeySymbol.F4,
            Key.F5 => KeySymbol.F5,
            Key.F6 => KeySymbol.F6,
            Key.F7 => KeySymbol.F7,
            Key.F8 => KeySymbol.F8,
            Key.F9 => KeySymbol.F9,
            Key.F10 => KeySymbol.F10,
            Key.F11 => KeySymbol.F11,
            Key.F12 => KeySymbol.F12,
            Key.LeftShift => KeySymbol.Shift_L,
            Key.RightShift => KeySymbol.Shift_R,
            Key.LeftCtrl => KeySymbol.Control_L,
            Key.RightCtrl => KeySymbol.Control_R,
            Key.LeftAlt => KeySymbol.Alt_L,
            Key.RightAlt => KeySymbol.Alt_R,
            _ => null,
        };
    }

    private void SetBitmap(PinnedBitmap bitmap)
    {
        DisposeBitmap();
        _currentBitmap = bitmap;
        ScreenImage.Source = bitmap.Bitmap;
    }

    private static PinnedBitmap CreatePinnedBitmap(VncFrameCapture frame)
    {
        var pixels = frame.Pixels;
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            // Pixels from TeacherVncSession are BGRA (see Teacher.Common VncFrameCapture).
            var bitmap = new Bitmap(
                Avalonia.Platform.PixelFormat.Bgra8888,
                AlphaFormat.Opaque,
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

    private static VncFrameCapture ResizeForViewer(VncFrameCapture frame, int maxWidth, int maxHeight)
    {
        if (frame.Width <= maxWidth && frame.Height <= maxHeight)
        {
            return frame;
        }

        var scale = Math.Min((double)maxWidth / frame.Width, (double)maxHeight / frame.Height);
        var targetWidth = Math.Max(1, (int)Math.Round(frame.Width * scale));
        var targetHeight = Math.Max(1, (int)Math.Round(frame.Height * scale));
        var targetPixels = new byte[targetWidth * targetHeight * 4];

        for (var y = 0; y < targetHeight; y++)
        {
            var sourceY = Math.Min(frame.Height - 1, (int)(y / scale));
            var sourceRow = sourceY * frame.Stride;
            var targetRow = y * targetWidth * 4;

            for (var x = 0; x < targetWidth; x++)
            {
                var sourceX = Math.Min(frame.Width - 1, (int)(x / scale));
                var sourceIndex = sourceRow + (sourceX * 4);
                var targetIndex = targetRow + (x * 4);

                targetPixels[targetIndex] = frame.Pixels[sourceIndex];
                targetPixels[targetIndex + 1] = frame.Pixels[sourceIndex + 1];
                targetPixels[targetIndex + 2] = frame.Pixels[sourceIndex + 2];
                targetPixels[targetIndex + 3] = frame.Pixels[sourceIndex + 3];
            }
        }

        return new VncFrameCapture(targetWidth, targetHeight, targetWidth * 4, targetPixels);
    }

}
