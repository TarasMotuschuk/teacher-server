using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using TeacherClient.CrossPlatform.Localization;
using TeacherClient.CrossPlatform.Services;

namespace TeacherClient.CrossPlatform;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            FfmpegBootstrap.TryConfigureBundledLibraries();
            FfmpegBootstrap.TryPreloadBundledFfmpegMacOS();
            var settings = new ClientSettingsStore().Load();
            CrossPlatformText.SetLanguage(settings.Language);
            AppUiThemeApplier.Apply(settings.Theme);
            var splash = new SplashWindow();
            splash.Icon = AppIconLoader.Load();
            desktop.MainWindow = splash;
            ShowMainWindowAfterSplashAsync(desktop, splash);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async void ShowMainWindowAfterSplashAsync(IClassicDesktopStyleApplicationLifetime desktop, SplashWindow splash)
    {
        await Task.Delay(900);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var mainWindow = new MainWindow();
            mainWindow.Icon = AppIconLoader.Load();
            desktop.MainWindow = mainWindow;
            mainWindow.Show();
            splash.Close();
        });
    }
}
