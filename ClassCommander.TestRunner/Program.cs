using Avalonia;

namespace ClassCommander.TestRunner;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        RunnerLaunchOptions.Apply(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
