using Avalonia.Controls;
using Teacher.Common.Contracts;
using TeacherClient.CrossPlatform.Localization;

namespace TeacherClient.CrossPlatform.Dialogs;

public partial class ProcessDetailsWindow : Window
{
    public ProcessActionRequested ActionRequested { get; private set; }

    public ProcessDetailsWindow()
    {
        InitializeComponent();
    }

    public ProcessDetailsWindow(ProcessDetailsDto details)
        : this()
    {
        Title = CrossPlatformText.ProcessDetailsTitle;
        KillButton.Content = CrossPlatformText.TerminateSelected;
        RestartButton.Content = CrossPlatformText.RestartSelected;
        CloseButton.Content = CrossPlatformText.Close;
        DetailsTextBox.Text = BuildText(details);
    }

    public static async Task<ProcessActionRequested> ShowAsync(Window owner, ProcessDetailsDto details)
    {
        var dialog = new ProcessDetailsWindow(details);
        await dialog.ShowDialog(owner);
        return dialog.ActionRequested;
    }

    private void KillButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ActionRequested = ProcessActionRequested.Kill;
        Close();
    }

    private void RestartButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ActionRequested = ProcessActionRequested.Restart;
        Close();
    }

    private void CloseButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ActionRequested = ProcessActionRequested.None;
        Close();
    }

    private static string BuildText(ProcessDetailsDto details)
    {
        return string.Join(
            Environment.NewLine,
        [
            $"PID: {details.Id}",
            $"{CrossPlatformText.ProcessLabel}: {details.Name}",
            $"{CrossPlatformText.Window}: {ValueOrFallback(details.MainWindowTitle)}",
            $"{CrossPlatformText.Visible}: {FormatBool(details.HasVisibleWindow)}",
            $"{CrossPlatformText.Responding}: {FormatBool(details.Responding)}",
            $"{CrossPlatformText.StartedUtc}: {details.StartTimeUtc:u}",
            $"{CrossPlatformText.Size}: {FormatBytes(details.WorkingSetBytes)}",
            $"{CrossPlatformText.ExecutablePath}: {ValueOrFallback(details.ExecutablePath)}",
            $"{CrossPlatformText.CommandLine}: {ValueOrFallback(details.CommandLine)}",
            $"{CrossPlatformText.SessionId}: {details.SessionId}",
            $"{CrossPlatformText.ThreadCount}: {details.ThreadCount}",
            $"{CrossPlatformText.HandleCount}: {details.HandleCount}",
            $"{CrossPlatformText.PriorityClass}: {ValueOrFallback(details.PriorityClass)}",
            $"{CrossPlatformText.TotalProcessorTime}: {details.TotalProcessorTime}",
            $"{CrossPlatformText.FileVersion}: {ValueOrFallback(details.FileVersion)}",
            $"{CrossPlatformText.ProductName}: {ValueOrFallback(details.ProductName)}",
            $"{CrossPlatformText.Error}: {ValueOrFallback(details.ErrorMessage)}"
        ]);
    }

    private static string ValueOrFallback(string? value)
        => string.IsNullOrWhiteSpace(value) ? CrossPlatformText.NotAvailable : value;

    private static string FormatBool(bool value)
        => value ? (CrossPlatformText.IsUk ? "Так" : "Yes") : (CrossPlatformText.IsUk ? "Ні" : "No");

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }
}

public enum ProcessActionRequested
{
    None,
    Kill,
    Restart,
}
