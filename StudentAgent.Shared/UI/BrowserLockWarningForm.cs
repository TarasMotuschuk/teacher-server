using System.Globalization;

namespace StudentAgent.UI;

public sealed class BrowserLockWarningForm : Form
{
    private readonly Label _countdownLabel;
    private readonly System.Windows.Forms.Timer _timer;
    private int _secondsRemaining;

    public BrowserLockWarningForm(int seconds)
    {
        _secondsRemaining = seconds;

        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(15, 23, 42);
        BackgroundImage = BrandingResourceLoader.LoadBitmap(@"Backgrounds/browser-lock.png");
        BackgroundImageLayout = ImageLayout.Stretch;
        Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        TopMost = true;
        Text = StudentAgent.UI.Localization.StudentAgentText.BrowserUsageForbiddenTitle;
        Width = 740;
        Height = 340;
        Padding = new Padding(0);

        _countdownLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 32F, FontStyle.Bold, GraphicsUnit.Point),
            Text = _secondsRemaining.ToString(CultureInfo.InvariantCulture),
            UseCompatibleTextRendering = false,
        };

        Controls.Add(_countdownLabel);
        Resize += (_, _) => PositionCountdownLabel();

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => UpdateCountdown();
        _timer.Start();
    }

    private void PositionCountdownLabel()
    {
        const int margin = 24;
        _countdownLabel.Location = new Point(
            margin,
            ClientSize.Height - _countdownLabel.Height - margin);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        PositionCountdownLabel();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void UpdateCountdown()
    {
        _secondsRemaining--;
        if (_secondsRemaining <= 0)
        {
            _timer.Stop();
            Close();
            return;
        }

        _countdownLabel.Text = _secondsRemaining.ToString(CultureInfo.InvariantCulture);
        PositionCountdownLabel();
    }
}
