namespace StudentAgent.UI;

public sealed class BrowserLockWarningForm : Form
{
    private readonly Label _messageLabel;
    private readonly Label _countdownLabel;
    private readonly System.Windows.Forms.Timer _timer;
    private int _secondsRemaining;

    public BrowserLockWarningForm(string message, int seconds)
    {
        _secondsRemaining = seconds;

        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(255, 248, 225);
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
        Padding = new Padding(24, 24, 24, 20);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(210, 255, 248, 225),
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

        _messageLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold, GraphicsUnit.Point),
            BackColor = Color.Transparent,
            Text = message
        };

        _countdownLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point)
            ,
            BackColor = Color.Transparent
        };

        layout.Controls.Add(_messageLabel, 0, 0);
        layout.Controls.Add(_countdownLabel, 0, 1);
        Controls.Add(layout);

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => UpdateCountdown();
        UpdateCountdownText();
        _timer.Start();
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

        UpdateCountdownText();
    }

    private void UpdateCountdownText()
    {
        _countdownLabel.Text = StudentAgent.UI.Localization.StudentAgentText.BrowserWillCloseIn(_secondsRemaining);
    }
}
