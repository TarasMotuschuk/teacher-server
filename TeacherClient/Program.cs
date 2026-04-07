using TeacherClient.Localization;
using TeacherClient.Services;

namespace TeacherClient;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) => ShowUnhandledError(args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            ShowUnhandledError(args.ExceptionObject as Exception);

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

    private static void ShowUnhandledError(Exception? exception)
    {
        if (exception is null)
        {
            return;
        }

        if (exception is OperationCanceledException)
        {
            return;
        }

        MessageBox.Show(
            exception.Message,
            TeacherClientText.MainTitle,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
