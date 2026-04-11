using StudentAgent.UI.Localization;

namespace StudentAgent.UI;

public sealed class InputLockBannerForm : Form
{
    private readonly System.Windows.Forms.Timer _topMostTimer;
    private bool _allowClose;

    public InputLockBannerForm(Screen screen)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(15, 23, 42);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ControlBox = false;
        MinimizeBox = false;
        MaximizeBox = false;
        Width = Math.Min(560, Math.Max(380, screen.WorkingArea.Width - 80));
        Height = 84;
        Left = screen.WorkingArea.Left + Math.Max(24, (screen.WorkingArea.Width - Width) / 2);
        Top = screen.WorkingArea.Top + 24;
        Text = StudentAgentText.InputLockDemoStatusLine;

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = StudentAgentText.InputLockDemoStatusLine,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.FromArgb(225, 15, 23, 42),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Regular, GraphicsUnit.Point),
            Padding = new Padding(18, 12, 18, 12),
        };

        Controls.Add(titleLabel);

        _topMostTimer = new System.Windows.Forms.Timer { Interval = 1500 };
        _topMostTimer.Tick += (_, _) =>
        {
            if (!Visible)
            {
                return;
            }

            TopMost = false;
            TopMost = true;
            BringToFront();
        };
        _topMostTimer.Start();
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
            return;
        }

        _topMostTimer.Stop();
        _topMostTimer.Dispose();
        base.OnFormClosing(e);
    }
}
