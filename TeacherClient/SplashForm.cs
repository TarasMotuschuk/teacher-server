using System.Drawing.Drawing2D;
using TeacherClient.Localization;

namespace TeacherClient;

internal sealed class SplashForm : Form
{
    public SplashForm()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(640, 320);
        BackColor = Color.FromArgb(15, 23, 42);
        DoubleBuffered = true;

        var titleLabel = new Label
        {
            AutoSize = false,
            Text = TeacherClientText.SplashTitle,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 28F, FontStyle.Bold, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(46, 82),
            Size = new Size(548, 56)
        };

        var subtitleLabel = new Label
        {
            AutoSize = false,
            Text = TeacherClientText.SplashSubtitle,
            ForeColor = Color.FromArgb(191, 219, 254),
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 12.5F, FontStyle.Regular, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(50, 152),
            Size = new Size(520, 34)
        };

        var progressBar = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Location = new Point(50, 234),
            Size = new Size(540, 18)
        };

        Controls.Add(titleLabel);
        Controls.Add(subtitleLabel);
        Controls.Add(progressBar);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(15, 23, 42), Color.FromArgb(30, 41, 59), LinearGradientMode.ForwardDiagonal);
        e.Graphics.FillRectangle(brush, ClientRectangle);

        using var accentBrush = new SolidBrush(Color.FromArgb(59, 130, 246));
        e.Graphics.FillRectangle(accentBrush, 0, 0, 12, Height);

        using var glowBrush = new SolidBrush(Color.FromArgb(36, 99, 235));
        e.Graphics.FillEllipse(glowBrush, Width - 190, 26, 120, 120);
        e.Graphics.FillEllipse(glowBrush, Width - 90, 120, 60, 60);
    }
}
