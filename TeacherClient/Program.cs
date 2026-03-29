using System.Windows.Forms;
using System.Threading;
using TeacherClient.Localization;
using TeacherClient.Services;

namespace TeacherClient;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var settings = new ClientSettingsStore().Load();
        TeacherClientText.SetLanguage(settings.Language);
        using (var splash = new SplashForm())
        {
            splash.Show();
            Application.DoEvents();
            Thread.Sleep(900);
            splash.Close();
        }
        Application.Run(new MainForm());
    }
}
