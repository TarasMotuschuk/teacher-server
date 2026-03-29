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
        DoubleBuffered = true;
        BackColor = Color.FromArgb(15, 23, 42);
        ClientSize = new Size(860, 460);
        MinimumSize = new Size(860, 460);
        Padding = new Padding(32, 32, 42, 32);

        var outerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        outerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 34F));
        outerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 36F));
        outerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = TeacherClientText.SplashTitle,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 34F, FontStyle.Bold, GraphicsUnit.Point),
            TextAlign = ContentAlignment.BottomLeft,
            MaximumSize = new Size(740, 0),
            Margin = new Padding(30, 18, 30, 0)
        };

        var subtitleLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = TeacherClientText.SplashSubtitle,
            ForeColor = Color.FromArgb(191, 219, 254),
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 17F, FontStyle.Regular, GraphicsUnit.Point),
            TextAlign = ContentAlignment.TopLeft,
            MaximumSize = new Size(700, 0),
            Margin = new Padding(34, 12, 40, 0)
        };

        var progressHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(34, 28, 34, 46)
        };

        var progressBar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 20,
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30
        };

        progressHost.Controls.Add(progressBar);
        outerLayout.Controls.Add(titleLabel, 0, 0);
        outerLayout.Controls.Add(subtitleLabel, 0, 1);
        outerLayout.Controls.Add(progressHost, 0, 2);
        Controls.Add(outerLayout);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(15, 23, 42), Color.FromArgb(30, 41, 59), LinearGradientMode.ForwardDiagonal);
        e.Graphics.FillRectangle(brush, ClientRectangle);

        using var accentBrush = new SolidBrush(Color.FromArgb(59, 130, 246));
        e.Graphics.FillRectangle(accentBrush, 0, 0, 14, Height);

        using var glowBrush = new SolidBrush(Color.FromArgb(36, 99, 235));
        e.Graphics.FillEllipse(glowBrush, Width - 270, 34, 170, 170);
        e.Graphics.FillEllipse(glowBrush, Width - 122, 154, 92, 92);
    }
}
