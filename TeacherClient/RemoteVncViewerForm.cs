#nullable enable

using System.Drawing.Imaging;
using Teacher.Common.Vnc;
using TeacherClient.Localization;
using KeySym = MarcusW.VncClient.KeySymbol;

namespace TeacherClient;

public sealed class RemoteVncViewerForm : Form
{
    private static readonly KeyboardShortcutOption[] ShortcutOptions =
    [
        new(TeacherClientText.SendKeyboardShortcut),
        new("Ctrl+Alt+Del", KeySym.Control_L, KeySym.Alt_L, KeySym.Delete),
        new("Ctrl+Shift+Esc", KeySym.Control_L, KeySym.Shift_L, KeySym.Escape),
        new("Alt+Tab", KeySym.Alt_L, KeySym.Tab),
        new("Alt+F4", KeySym.Alt_L, KeySym.F4),
        new("Win", KeySym.Super_L),
        new("Win+Tab", KeySym.Super_L, KeySym.Tab),
        new("Win+R", KeySym.Super_L, KeySym.R),
        new("Win+D", KeySym.Super_L, KeySym.D),
        new("Ctrl+Esc", KeySym.Control_L, KeySym.Escape),
        new("Print Screen", KeySym.Print)
    ];

    private readonly TeacherVncSession _session;
    private readonly bool _ownsSession;
    private readonly Panel _imagePanel;
    private readonly PictureBox _pictureBox;
    private readonly Label _statusLabel;
    private readonly ComboBox _sendShortcutComboBox;
    private readonly Button _enableControlButton;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new();
    private readonly string _machineName;
    private int _captureInProgress;
    private int _connectInProgress;
    private int _frameWidth;
    private int _frameHeight;
    private bool _updatingShortcutSelection;

    public RemoteVncViewerForm(string machineName, string host, int port, string sharedSecret, bool controlEnabled)
        : this(machineName, new TeacherVncSession(host, port, sharedSecret, controlEnabled), ownsSession: true)
    {
    }

    public RemoteVncViewerForm(string machineName, TeacherVncSession session, bool ownsSession = false)
    {
        _machineName = machineName;
        _session = session;
        _ownsSession = ownsSession;

        Icon = AppIconLoader.Load();
        Text = TeacherClientText.RemoteManagementViewerTitle(machineName);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        BackColor = Color.Black;
        MinimumSize = new Size(800, 600);

        _imagePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            TabStop = true,
        };

        _pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,

            // Zoom preserves aspect ratio (matches Avalonia Uniform); StretchImage can distort.
            SizeMode = PictureBoxSizeMode.Zoom,
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.WhiteSmoke,
            BackColor = Color.FromArgb(60, 60, 60),
            Padding = new Padding(10, 0, 10, 0),
        };

        _enableControlButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = TeacherClientText.EnableFullscreenControl,
            Visible = !_session.ControlEnabled,
        };
        _enableControlButton.Click += (_, _) =>
        {
            _session.ControlEnabled = true;
            UpdateControlUi();
            _imagePanel.Focus();
        };

        _sendShortcutComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _sendShortcutComboBox.Items.AddRange(ShortcutOptions);
        _sendShortcutComboBox.SelectedIndex = 0;
        _sendShortcutComboBox.SelectedIndexChanged += async (_, _) =>
        {
            if (_updatingShortcutSelection || _sendShortcutComboBox.SelectedItem is not KeyboardShortcutOption option || option.Keys.Length == 0)
            {
                return;
            }

            _session.ControlEnabled = true;
            UpdateControlUi();

            try
            {
                await _session.SendKeyCombinationAsync(option.Keys);
            }
            finally
            {
                _updatingShortcutSelection = true;
                _sendShortcutComboBox.SelectedIndex = 0;
                _updatingShortcutSelection = false;
                _imagePanel.Focus();
            }
        };

        var bottomBar = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            BackColor = Color.FromArgb(60, 60, 60),
            Padding = new Padding(8, 4, 8, 4),
            ColumnCount = 3,
            RowCount = 1,
        };
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 248F));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 178F));
        bottomBar.Controls.Add(_statusLabel, 0, 0);
        bottomBar.Controls.Add(_sendShortcutComboBox, 1, 0);
        bottomBar.Controls.Add(_enableControlButton, 2, 0);

        _imagePanel.Controls.Add(_pictureBox);

        // Dock Bottom first so the fill panel receives the remaining client height above the toolbar.
        Controls.Add(bottomBar);
        Controls.Add(_imagePanel);

        _refreshTimer.Interval = 500;
        _refreshTimer.Tick += async (_, _) => await CaptureFrameAsync();

        Shown += async (_, _) =>
        {
            await ConnectAsync();
            if (!_cancellation.IsCancellationRequested && !IsDisposed)
            {
                _refreshTimer.Start();

                // PictureBox cannot take focus; keyboard events must reach a focused control — not only the Form.
                _imagePanel.Focus();
            }
        };
        FormClosing += (_, _) =>
        {
            _refreshTimer.Stop();
            _cancellation.Cancel();

            // Capture/Connect run on thread pool; disposing the session while they touch the render
            // target or connection can fault the process.
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

            DisposePicture();
        };
        FormClosed += (_, _) =>
        {
            _cancellation.Dispose();
        };

        _pictureBox.MouseDown += PictureBox_MouseDown;
        _pictureBox.MouseUp += PictureBox_MouseUp;
        _pictureBox.MouseMove += PictureBox_MouseMove;
        _pictureBox.MouseWheel += PictureBox_MouseWheel;
        _imagePanel.KeyDown += ImagePanel_KeyDown;
        _imagePanel.KeyUp += ImagePanel_KeyUp;
        _imagePanel.KeyPress += ImagePanel_KeyPress;
        UpdateControlUi();
    }

    private async Task ConnectAsync()
    {
        Interlocked.Increment(ref _connectInProgress);
        try
        {
            try
            {
                _statusLabel.Text = TeacherClientText.RemoteManagementConnecting(_machineName);
                if (!_session.IsConnected)
                {
                    // Handshake can run synchronously for long stretches; keep UI responsive.
                    // Do not pass CTS into Task.Run — cancel would fault the outer task and surface OCE to async void handlers.
                    await Task.Run(async () => await _session.ConnectAsync(_cancellation.Token));
                }

                if (IsDisposed)
                {
                    return;
                }

                UpdateControlUi();
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    _statusLabel.Text = TeacherClientText.RemoteManagementConnectionFailed(_machineName, ex.Message);
                }
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
            // Timer runs on the UI thread; Capture + RGBA→BGRA + GDI+ bitmap are CPU-heavy and must not block it.
            var (bitmap, frameW, frameH) = await Task.Run(
                async () =>
                {
                    var frame = await _session.CaptureFrameAsync(_cancellation.Token);
                    if (frame is null)
                    {
                        return ((Bitmap?)null, 0, 0);
                    }

                    var resized = ResizeForViewer(frame, VncViewerDisplayLimits.MaxFrameWidth, VncViewerDisplayLimits.MaxFrameHeight);
                    return (CreateBitmap(resized), frame.Width, frame.Height);
                },
                _cancellation.Token);

            if (bitmap is null)
            {
                return;
            }

            if (IsDisposed)
            {
                bitmap.Dispose();
                return;
            }

            _frameWidth = frameW;
            _frameHeight = frameH;
            SetPicture(bitmap);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                _statusLabel.Text = TeacherClientText.RemoteManagementConnectionFailed(_machineName, ex.Message);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _captureInProgress, 0);
        }
    }

    private void SetPicture(Bitmap bitmap)
    {
        DisposePicture();
        _pictureBox.Image = bitmap;
    }

    private void UpdateControlUi()
    {
        _statusLabel.Text = _session.ControlEnabled
            ? TeacherClientText.RemoteManagementControl(_machineName)
            : TeacherClientText.RemoteManagementViewOnly(_machineName);
        _enableControlButton.Visible = !_session.ControlEnabled;
    }

    private void DisposePicture()
    {
        if (_pictureBox.Image is not null)
        {
            var image = _pictureBox.Image;
            _pictureBox.Image = null;
            image.Dispose();
        }
    }

    private sealed record KeyboardShortcutOption(string Label, params KeySym[] Keys)
    {
        public override string ToString() => Label;
    }

    private void PictureBox_MouseDown(object? sender, MouseEventArgs e)
    {
        _imagePanel.Focus();

        if (!_session.ControlEnabled || _frameWidth <= 0 || _frameHeight <= 0)
        {
            return;
        }

        if (!TryMapPointerToRemote(e.X, e.Y, out var rx, out var ry))
        {
            return;
        }

        try
        {
            _session.SendPointer(rx, ry, ButtonsMask(e.Button, true));
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void PictureBox_MouseUp(object? sender, MouseEventArgs e)
    {
        if (!_session.ControlEnabled || _frameWidth <= 0 || _frameHeight <= 0)
        {
            return;
        }

        if (!TryMapPointerToRemote(e.X, e.Y, out var rx, out var ry))
        {
            return;
        }

        try
        {
            _session.SendPointer(rx, ry, ButtonsMask(e.Button, false));
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void PictureBox_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_session.ControlEnabled || _frameWidth <= 0 || _frameHeight <= 0)
        {
            return;
        }

        if (!TryMapPointerToRemote(e.X, e.Y, out var rx, out var ry))
        {
            return;
        }

        try
        {
            if ((e.Button & MouseButtons.Left) == 0 &&
                (e.Button & MouseButtons.Right) == 0 &&
                (e.Button & MouseButtons.Middle) == 0)
            {
                _session.SendPointer(rx, ry, 0);
                return;
            }

            _session.SendPointer(rx, ry, ButtonsMask(e.Button, true));
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void PictureBox_MouseWheel(object? sender, MouseEventArgs e)
    {
        if (!_session.ControlEnabled || _frameWidth <= 0 || _frameHeight <= 0)
        {
            return;
        }

        if (!TryMapPointerToRemote(e.X, e.Y, out var rx, out var ry))
        {
            return;
        }

        try
        {
            var buttons = e.Delta > 0 ? 8 : 16;
            _session.SendPointer(rx, ry, buttons);
            _session.SendPointer(rx, ry, 0);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void ImagePanel_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!_session.ControlEnabled)
        {
            return;
        }

        try
        {
            var keySym = MapSpecialKey(e.KeyCode);
            if (keySym is not null)
            {
                _session.SendKey(keySym.Value, true);
                e.Handled = true;
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void ImagePanel_KeyUp(object? sender, KeyEventArgs e)
    {
        if (!_session.ControlEnabled)
        {
            return;
        }

        try
        {
            var keySym = MapSpecialKey(e.KeyCode);
            if (keySym is not null)
            {
                _session.SendKey(keySym.Value, false);
                e.Handled = true;
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void ImagePanel_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!_session.ControlEnabled || char.IsControl(e.KeyChar))
        {
            return;
        }

        try
        {
            var keySym = (KeySym)(uint)e.KeyChar;
            _session.SendKey(keySym, true);
            _session.SendKey(keySym, false);
            e.Handled = true;
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    // Client rect → remote framebuffer; matches PictureBox Zoom (same math as Avalonia TryMapPointer).
    private bool TryMapPointerToRemote(int clientX, int clientY, out int x, out int y)
    {
        var width = Math.Max(1, _pictureBox.ClientSize.Width);
        var height = Math.Max(1, _pictureBox.ClientSize.Height);

        var scale = Math.Min(width / (double)_frameWidth, height / (double)_frameHeight);
        var displayedWidth = _frameWidth * scale;
        var displayedHeight = _frameHeight * scale;
        var offsetX = (width - displayedWidth) / 2.0;
        var offsetY = (height - displayedHeight) / 2.0;

        if (clientX < offsetX || clientY < offsetY ||
            clientX > offsetX + displayedWidth || clientY > offsetY + displayedHeight)
        {
            x = 0;
            y = 0;
            return false;
        }

        x = Math.Clamp((int)Math.Round((clientX - offsetX) / scale), 0, _frameWidth - 1);
        y = Math.Clamp((int)Math.Round((clientY - offsetY) / scale), 0, _frameHeight - 1);
        return true;
    }

    private static int ButtonsMask(MouseButtons button, bool pressed)
    {
        var mask = button switch
        {
            MouseButtons.Left => 1,
            MouseButtons.Middle => 2,
            MouseButtons.Right => 4,
            _ => 0,
        };

        return pressed ? mask : 0;
    }

    private static KeySym? MapSpecialKey(Keys key)
    {
        return key switch
        {
            Keys.Back => KeySym.BackSpace,
            Keys.Tab => KeySym.Tab,
            Keys.Return => KeySym.Return,
            Keys.Escape => KeySym.Escape,
            Keys.PageUp => KeySym.Page_Up,
            Keys.PageDown => KeySym.Page_Down,
            Keys.End => KeySym.End,
            Keys.Home => KeySym.Home,
            Keys.Left => KeySym.Left,
            Keys.Up => KeySym.Up,
            Keys.Right => KeySym.Right,
            Keys.Down => KeySym.Down,
            Keys.Insert => KeySym.Insert,
            Keys.Delete => KeySym.Delete,
            Keys.F1 => KeySym.F1,
            Keys.F2 => KeySym.F2,
            Keys.F3 => KeySym.F3,
            Keys.F4 => KeySym.F4,
            Keys.F5 => KeySym.F5,
            Keys.F6 => KeySym.F6,
            Keys.F7 => KeySym.F7,
            Keys.F8 => KeySym.F8,
            Keys.F9 => KeySym.F9,
            Keys.F10 => KeySym.F10,
            Keys.F11 => KeySym.F11,
            Keys.F12 => KeySym.F12,
            Keys.ShiftKey => KeySym.Shift_L,
            Keys.ControlKey => KeySym.Control_L,
            Keys.Menu => KeySym.Alt_L,
            _ => null,
        };
    }

    private static Bitmap CreateBitmap(VncFrameCapture frame)
    {
        var bitmap = new Bitmap(frame.Width, frame.Height, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, frame.Width, frame.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            VncBgraBitmapUtils.CopyTightBgraToLockedBitmap(frame.Pixels, frame.Width, frame.Height, frame.Stride, data);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
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
