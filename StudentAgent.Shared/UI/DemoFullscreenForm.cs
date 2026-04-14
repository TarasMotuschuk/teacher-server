using StudentAgent.UI.Localization;

namespace StudentAgent.UI;

public sealed class DemoFullscreenForm : Form
{
    private readonly System.Windows.Forms.Timer _focusTimer;
    private readonly Label _bannerLabel;
    private readonly Panel _videoHost;
    private readonly PictureBox _pictureBox;
    private bool _allowClose;
    private bool _disposed;
    private bool _bringPosted;

    public DemoFullscreenForm(Screen screen)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.Black;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Bounds = screen.Bounds;
        TopMost = true;
        ControlBox = false;
        MinimizeBox = false;
        MaximizeBox = false;
        KeyPreview = true;
        Text = StudentAgentText.InputLockDemoStatusLine;

        _bannerLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 56,
            Text = StudentAgentText.InputLockDemoStatusLine,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.FromArgb(225, 15, 23, 42),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Regular, GraphicsUnit.Point),
        };

        _videoHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
        };

        _pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Black,
        };

        _videoHost.Controls.Add(_pictureBox);
        Controls.Add(_videoHost);
        Controls.Add(_bannerLabel);

        _focusTimer = new System.Windows.Forms.Timer { Interval = 750 };
        _focusTimer.Tick += (_, _) => ScheduleBringToFront();
        _focusTimer.Start();

        Shown += (_, _) => ScheduleBringToFront();
    }

    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }

    public void SetBannerText(string text)
    {
        _bannerLabel.Text = text;
        Text = text;
    }

    public void SetFrame(Bitmap bitmap)
    {
        var old = _pictureBox.Image;
        _pictureBox.Image = bitmap;
        old?.Dispose();
    }

    // Placeholder hook for a future video renderer control.
    public Control VideoHost => _videoHost;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            ScheduleBringToFront();
            return;
        }

        _focusTimer.Stop();
        _focusTimer.Dispose();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            base.Dispose(disposing);
            return;
        }

        _disposed = true;
        if (disposing)
        {
            _focusTimer.Stop();
            _focusTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        return true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        ScheduleBringToFront();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        ScheduleBringToFront();
    }

    private void ScheduleBringToFront()
    {
        if (_disposed || IsDisposed || Disposing || !IsHandleCreated || !Visible)
        {
            return;
        }

        if (_bringPosted)
        {
            return;
        }

        _bringPosted = true;
        try
        {
            BeginInvoke(() =>
            {
                try
                {
                    if (!TopMost)
                    {
                        TopMost = true;
                    }

                    BringToFront();
                }
                finally
                {
                    _bringPosted = false;
                }
            });
        }
        catch
        {
            _bringPosted = false;
        }
    }
}
