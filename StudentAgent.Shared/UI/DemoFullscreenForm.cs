using StudentAgent.UI.Localization;

namespace StudentAgent.UI;

public sealed class DemoFullscreenForm : Form
{
    private readonly System.Windows.Forms.Timer _focusTimer;
    private readonly System.Windows.Forms.Timer _watchdogTimer;
    private readonly Label _bannerLabel;
    private readonly Panel _videoHost;
    private readonly PictureBox _pictureBox;
    private bool _allowClose;
    private bool _disposed;
    private bool _bringPosted;
    private DateTimeOffset _lastFrameUtc;
    private bool _noSignalShown;

    private const int WatchdogIntervalMs = 1000;
    private static readonly TimeSpan NoSignalAfter = TimeSpan.FromSeconds(20);
    private const Keys EmergencyExitChord = Keys.Control | Keys.Alt | Keys.Shift | Keys.Q;

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

        _lastFrameUtc = DateTimeOffset.UtcNow;
        _watchdogTimer = new System.Windows.Forms.Timer { Interval = WatchdogIntervalMs };
        _watchdogTimer.Tick += (_, _) => WatchdogTick();
        _watchdogTimer.Start();

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
        _lastFrameUtc = DateTimeOffset.UtcNow;
        if (_noSignalShown)
        {
            _noSignalShown = false;
            _bannerLabel.Text = StudentAgentText.InputLockDemoStatusLine;
            Text = _bannerLabel.Text;
        }

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
        _watchdogTimer.Stop();
        _watchdogTimer.Dispose();
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
            _watchdogTimer.Stop();
            _watchdogTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == EmergencyExitChord)
        {
            ForceClose();
            return true;
        }

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

    private void WatchdogTick()
    {
        if (_disposed || IsDisposed || Disposing || !IsHandleCreated || !Visible)
        {
            return;
        }

        if (_noSignalShown)
        {
            return;
        }

        var age = DateTimeOffset.UtcNow - _lastFrameUtc;
        if (age < NoSignalAfter)
        {
            return;
        }

        _noSignalShown = true;
        var msg = $"{StudentAgentText.InputLockDemoStatusLine} — no video signal. Emergency exit: Ctrl+Alt+Shift+Q.";
        _bannerLabel.Text = msg;
        Text = msg;
    }
}
