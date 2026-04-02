using System.Reflection;
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

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("TeacherClient.Assets.ClassCommander-splash.png");
        if (stream is not null)
        {
            using var image = Image.FromStream(stream);
            pictureBox.Image = new Bitmap(image);
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
