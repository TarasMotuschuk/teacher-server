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
        BackColor = Color.Black;
        ClientSize = new Size(960, 540);
        MinimumSize = new Size(960, 540);

        var pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.StretchImage,
            BackColor = Color.Black
        };

        var splashImage = BrandingResourceLoader.LoadBitmap("ClassCommander-splash.png");
        if (splashImage is not null)
        {
            pictureBox.Image = new Bitmap(splashImage);
        }
        else
        {
            var fallbackLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = TeacherClientText.SplashTitle,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(15, 23, 42),
                Font = new Font("Segoe UI", 32F, FontStyle.Bold, GraphicsUnit.Point),
                TextAlign = ContentAlignment.MiddleCenter
            };

            Controls.Add(fallbackLabel);
            return;
        }

        Controls.Add(pictureBox);
    }
}
