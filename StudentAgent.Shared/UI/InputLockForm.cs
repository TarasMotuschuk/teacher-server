using StudentAgent.UI.Localization;

namespace StudentAgent.UI;

public sealed class InputLockForm : Form
{
    private readonly System.Windows.Forms.Timer _focusTimer;
    private bool _allowClose;
    private bool _disposed;
    private bool _bringPosted;

    public InputLockForm(Screen screen)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(15, 23, 42);
        BackgroundImage = BrandingResourceLoader.LoadBitmap(@"Backgrounds/input-lock.png");
        BackgroundImageLayout = ImageLayout.Stretch;
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
        Text = StudentAgentText.InputLockStatusLine;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(170, 15, 23, 42),
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(48, 48, 48, 32)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var spacer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        var statusLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = StudentAgentText.InputLockStatusLine,
            Font = new Font("Segoe UI", 20F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.BottomCenter
        };

        layout.Controls.Add(spacer, 0, 0);
        layout.Controls.Add(statusLabel, 0, 1);
        Controls.Add(layout);

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

    // Deferred z-order only: synchronous Activate/Focus from mouse/timer + Activated caused UI-thread hangs.
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
                    BringToFrontCore();
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

    private void BringToFrontCore()
    {
        if (_disposed || IsDisposed || Disposing || !Visible)
        {
            return;
        }

        try
        {
            if (!TopMost)
            {
                TopMost = true;
            }

            BringToFront();
        }
        catch
        {
        }
    }
}
