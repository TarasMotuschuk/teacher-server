#nullable enable

using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using Teacher.Common;
using Teacher.Common.Vnc;
using TeacherClient.Localization;
using KeySym = MarcusW.VncClient.KeySymbol;

namespace TeacherClient;

public sealed class RemoteVncViewerForm : Form
{
    private readonly TeacherVncSession _session;
    private readonly bool _ownsSession;
    private readonly Panel _contentPanel;
    private readonly PictureBox _pictureBox;
    private readonly Label _statusLabel;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new();
    private readonly string _machineName;
    private int _captureInProgress;
    private int _connectInProgress;
    private int _frameWidth;
    private int _frameHeight;

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

        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            TabStop = true
        };

        _pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            // Zoom preserves aspect ratio (matches Avalonia Uniform); StretchImage can distort.
            SizeMode = PictureBoxSizeMode.Zoom
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 30,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.WhiteSmoke,
            BackColor = Color.FromArgb(60, 60, 60),
            Padding = new Padding(10, 0, 10, 0)
        };

        _contentPanel.Controls.Add(_pictureBox);
        _contentPanel.Controls.Add(_statusLabel);

        Controls.Add(_contentPanel);

        _refreshTimer.Interval = 500;
        _refreshTimer.Tick += async (_, _) => await CaptureFrameAsync();

        Shown += async (_, _) =>
        {
            await ConnectAsync();
            if (!_cancellation.IsCancellationRequested && !IsDisposed)
            {
                _refreshTimer.Start();
                // PictureBox cannot take focus; keyboard events must reach a focused control — not only the Form.
                _contentPanel.Focus();
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
        _contentPanel.KeyDown += ContentPanel_KeyDown;
        _contentPanel.KeyUp += ContentPanel_KeyUp;
        _contentPanel.KeyPress += ContentPanel_KeyPress;

        _statusLabel.Text = _session.ControlEnabled
            ? TeacherClientText.RemoteManagementControl(machineName)
            : TeacherClientText.RemoteManagementViewOnly(machineName);
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

                _statusLabel.Text = _session.ControlEnabled
                    ? TeacherClientText.RemoteManagementControl(_machineName)
                    : TeacherClientText.RemoteManagementViewOnly(_machineName);
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

    private void DisposePicture()
    {
        if (_pictureBox.Image is not null)
        {
            var image = _pictureBox.Image;
            _pictureBox.Image = null;
            image.Dispose();
        }
    }

    private void PictureBox_MouseDown(object? sender, MouseEventArgs e)
    {
        _contentPanel.Focus();

        if (!_session.ControlEnabled || _frameWidth <= 0 || _frameHeight <= 0)
        {
            return;
        }

        try
        {
            _session.SendPointer(MapX(e.X), MapY(e.Y), ButtonsMask(e.Button, true));
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

        try
        {
            _session.SendPointer(MapX(e.X), MapY(e.Y), ButtonsMask(e.Button, false));
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

        try
        {
            if ((e.Button & MouseButtons.Left) == 0 &&
                (e.Button & MouseButtons.Right) == 0 &&
                (e.Button & MouseButtons.Middle) == 0)
            {
                _session.SendPointer(MapX(e.X), MapY(e.Y), 0);
                return;
            }

            _session.SendPointer(MapX(e.X), MapY(e.Y), ButtonsMask(e.Button, true));
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

        try
        {
            var buttons = e.Delta > 0 ? 8 : 16;
            _session.SendPointer(MapX(e.X), MapY(e.Y), buttons);
            _session.SendPointer(MapX(e.X), MapY(e.Y), 0);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void ContentPanel_KeyDown(object? sender, KeyEventArgs e)
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

    private void ContentPanel_KeyUp(object? sender, KeyEventArgs e)
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

    private void ContentPanel_KeyPress(object? sender, KeyPressEventArgs e)
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

    private int MapX(int x)
    {
        var width = Math.Max(1, _pictureBox.ClientSize.Width);
        return Math.Max(0, Math.Min(_frameWidth - 1, (int)Math.Round(x * (_frameWidth / (double)width))));
    }

    private int MapY(int y)
    {
        var height = Math.Max(1, _pictureBox.ClientSize.Height);
        return Math.Max(0, Math.Min(_frameHeight - 1, (int)Math.Round(y * (_frameHeight / (double)height))));
    }

    private static int ButtonsMask(MouseButtons button, bool pressed)
    {
        var mask = button switch
        {
            MouseButtons.Left => 1,
            MouseButtons.Middle => 2,
            MouseButtons.Right => 4,
            _ => 0
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
            _ => null
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
