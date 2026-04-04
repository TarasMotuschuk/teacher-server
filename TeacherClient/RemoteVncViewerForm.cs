#nullable enable

using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Teacher.Common;
using Teacher.Common.Vnc;
using TeacherClient.Localization;
using KeySym = RemoteViewing.Vnc.KeySym;

namespace TeacherClient;

public sealed class RemoteVncViewerForm : Form
{
    private readonly TeacherVncSession _session;
    private readonly PictureBox _pictureBox;
    private readonly Label _statusLabel;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new();
    private readonly string _machineName;
    private int _frameWidth;
    private int _frameHeight;

    public RemoteVncViewerForm(string machineName, string host, int port, string sharedSecret, bool controlEnabled)
    {
        _machineName = machineName;
        _session = new TeacherVncSession(host, port, sharedSecret, controlEnabled);

        Icon = AppIconLoader.Load();
        Text = TeacherClientText.RemoteManagementViewerTitle(machineName);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        KeyPreview = true;
        BackColor = Color.Black;
        MinimumSize = new Size(800, 600);

        _pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            SizeMode = PictureBoxSizeMode.StretchImage
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

        var root = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black
        };
        root.Controls.Add(_pictureBox);
        root.Controls.Add(_statusLabel);

        Controls.Add(root);

        _refreshTimer.Interval = 500;
        _refreshTimer.Tick += async (_, _) => await CaptureFrameAsync();

        Shown += async (_, _) =>
        {
            await ConnectAsync();
            _refreshTimer.Start();
        };
        FormClosing += (_, _) =>
        {
            _refreshTimer.Stop();
            _cancellation.Cancel();
            _session.Dispose();
            _cancellation.Dispose();
            DisposePicture();
        };

        _pictureBox.MouseDown += PictureBox_MouseDown;
        _pictureBox.MouseUp += PictureBox_MouseUp;
        _pictureBox.MouseMove += PictureBox_MouseMove;
        _pictureBox.MouseWheel += PictureBox_MouseWheel;
        KeyDown += RemoteVncViewerForm_KeyDown;
        KeyUp += RemoteVncViewerForm_KeyUp;
        KeyPress += RemoteVncViewerForm_KeyPress;

        _statusLabel.Text = controlEnabled
            ? TeacherClientText.RemoteManagementControl(machineName)
            : TeacherClientText.RemoteManagementViewOnly(machineName);
    }

    private async Task ConnectAsync()
    {
        try
        {
            _statusLabel.Text = TeacherClientText.RemoteManagementConnecting(_machineName);
            await _session.ConnectAsync(_cancellation.Token);
            _statusLabel.Text = _session.ControlEnabled
                ? TeacherClientText.RemoteManagementControl(_machineName)
                : TeacherClientText.RemoteManagementViewOnly(_machineName);
            await CaptureFrameAsync();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = TeacherClientText.RemoteManagementConnectionFailed(_machineName, ex.Message);
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

            var bitmap = CreateBitmap(frame);
            if (IsDisposed)
            {
                bitmap.Dispose();
                return;
            }

            if (_pictureBox.InvokeRequired)
            {
                _pictureBox.BeginInvoke(new Action(() => SetPicture(bitmap)));
                return;
            }

            SetPicture(bitmap);
            _frameWidth = frame.Width;
            _frameHeight = frame.Height;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _statusLabel.Text = TeacherClientText.RemoteManagementConnectionFailed(_machineName, ex.Message);
        }
    }

    private void SetPicture(Bitmap bitmap)
    {
        DisposePicture();
        _pictureBox.Image = bitmap;
        _frameWidth = bitmap.Width;
        _frameHeight = bitmap.Height;
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
        if (!_session.ControlEnabled || _frameWidth <= 0 || _frameHeight <= 0)
        {
            return;
        }

        _session.SendPointer(MapX(e.X), MapY(e.Y), ButtonsMask(e.Button, true));
    }

    private void PictureBox_MouseUp(object? sender, MouseEventArgs e)
    {
        if (!_session.ControlEnabled || _frameWidth <= 0 || _frameHeight <= 0)
        {
            return;
        }

        _session.SendPointer(MapX(e.X), MapY(e.Y), ButtonsMask(e.Button, false));
    }

    private void PictureBox_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_session.ControlEnabled || _frameWidth <= 0 || _frameHeight <= 0)
        {
            return;
        }

        if ((e.Button & MouseButtons.Left) == 0 &&
            (e.Button & MouseButtons.Right) == 0 &&
            (e.Button & MouseButtons.Middle) == 0)
        {
            _session.SendPointer(MapX(e.X), MapY(e.Y), 0);
            return;
        }

        _session.SendPointer(MapX(e.X), MapY(e.Y), ButtonsMask(e.Button, true));
    }

    private void PictureBox_MouseWheel(object? sender, MouseEventArgs e)
    {
        if (!_session.ControlEnabled || _frameWidth <= 0 || _frameHeight <= 0)
        {
            return;
        }

        var buttons = e.Delta > 0 ? 8 : 16;
        _session.SendPointer(MapX(e.X), MapY(e.Y), buttons);
        _session.SendPointer(MapX(e.X), MapY(e.Y), 0);
    }

    private void RemoteVncViewerForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!_session.ControlEnabled)
        {
            return;
        }

        var keySym = MapSpecialKey(e.KeyCode);
        if (keySym is not null)
        {
            _session.SendKey(keySym.Value, true);
            e.Handled = true;
        }
    }

    private void RemoteVncViewerForm_KeyUp(object? sender, KeyEventArgs e)
    {
        if (!_session.ControlEnabled)
        {
            return;
        }

        var keySym = MapSpecialKey(e.KeyCode);
        if (keySym is not null)
        {
            _session.SendKey(keySym.Value, false);
            e.Handled = true;
        }
    }

    private void RemoteVncViewerForm_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!_session.ControlEnabled || char.IsControl(e.KeyChar))
        {
            return;
        }

        var keySym = RemoteViewing.Vnc.KeySymHelpers.FromChar(e.KeyChar);
        _session.SendKey(keySym, true);
        _session.SendKey(keySym, false);
        e.Handled = true;
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

    private static RemoteViewing.Vnc.KeySym? MapSpecialKey(Keys key)
    {
        return key switch
        {
            Keys.Back => KeySym.Backspace,
            Keys.Tab => KeySym.Tab,
            Keys.Return => KeySym.Return,
            Keys.Escape => KeySym.Escape,
            Keys.Space => KeySym.Space,
            Keys.PageUp => KeySym.PageUp,
            Keys.PageDown => KeySym.PageDown,
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
            Keys.ShiftKey => KeySym.ShiftLeft,
            Keys.ControlKey => KeySym.ControlLeft,
            Keys.Menu => KeySym.AltLeft,
            _ when key >= Keys.A && key <= Keys.Z => RemoteViewing.Vnc.KeySymHelpers.FromChar((char)('a' + (key - Keys.A))),
            _ when key >= Keys.D0 && key <= Keys.D9 => RemoteViewing.Vnc.KeySymHelpers.FromChar((char)('0' + (key - Keys.D0))),
            _ => null
        };
    }

    private static Bitmap CreateBitmap(VncFrameCapture frame)
    {
        var bitmap = new Bitmap(frame.Width, frame.Height, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, frame.Width, frame.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(frame.Pixels, 0, data.Scan0, Math.Min(frame.Pixels.Length, frame.Stride * frame.Height));
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }
}
