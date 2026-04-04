using StudentAgent.UI.Localization;

namespace StudentAgent.UI;

public sealed class InputLockForm : Form
{
    private readonly System.Windows.Forms.Timer _focusTimer;
    private bool _allowClose;

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
        Text = StudentAgentText.InputLockTitle;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(170, 15, 23, 42),
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(48)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));

        var titleLabel = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.None,
            Text = StudentAgentText.InputLockTitle,
            Font = new Font("Segoe UI", 28F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 24)
        };

        var messageLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(900, 0),
            Anchor = AnchorStyles.Top,
            Text = $"{StudentAgentText.InputLockMessage}{Environment.NewLine}{Environment.NewLine}{StudentAgentText.InputLockFooter}",
            Font = new Font("Segoe UI", 17F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter
        };

        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(messageLabel, 0, 1);
        Controls.Add(layout);

        _focusTimer = new System.Windows.Forms.Timer { Interval = 750 };
        _focusTimer.Tick += (_, _) => BringBackToFront();
        _focusTimer.Start();

        Shown += (_, _) => BringBackToFront();
        Activated += (_, _) => BringBackToFront();
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
            BringBackToFront();
            return;
        }

        _focusTimer.Stop();
        _focusTimer.Dispose();
        base.OnFormClosing(e);
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
        BringBackToFront();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        BringBackToFront();
    }

    private void BringBackToFront()
    {
        if (!Visible)
        {
            return;
        }

        TopMost = true;
        Activate();
        BringToFront();
        Focus();
    }
}
