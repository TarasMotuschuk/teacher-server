using Teacher.Common.Contracts;
using TeacherClient.Localization;

namespace TeacherClient;

public partial class ProcessDetailsDialog : Form
{
    public ProcessActionRequested ActionRequested { get; private set; }

    public ProcessDetailsDialog(ProcessDetailsDto details)
    {
        InitializeComponent();
        Icon = AppIconLoader.Load();
        Text = TeacherClientText.ProcessDetailsTitle;
        detailsTextBox.Text = BuildText(details);
    }

    private void KillButton_Click(object? sender, EventArgs e)
    {
        ActionRequested = ProcessActionRequested.Kill;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void RestartButton_Click(object? sender, EventArgs e)
    {
        ActionRequested = ProcessActionRequested.Restart;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static string BuildText(ProcessDetailsDto details)
    {
        return string.Join(
            Environment.NewLine,
            [
            $"PID: {details.Id}",
            $"{TeacherClientText.Process}: {details.Name}",
            $"{TeacherClientText.Window}: {ValueOrFallback(details.MainWindowTitle)}",
            $"{TeacherClientText.Visible}: {FormatBool(details.HasVisibleWindow)}",
            $"{TeacherClientText.Responding}: {FormatBool(details.Responding)}",
            $"{TeacherClientText.StartedUtc}: {details.StartTimeUtc:u}",
            $"{TeacherClientText.Size}: {FormatBytes(details.WorkingSetBytes)}",
            $"{TeacherClientText.ExecutablePath}: {ValueOrFallback(details.ExecutablePath)}",
            $"{TeacherClientText.CommandLine}: {ValueOrFallback(details.CommandLine)}",
            $"{TeacherClientText.SessionId}: {details.SessionId}",
            $"{TeacherClientText.ThreadCount}: {details.ThreadCount}",
            $"{TeacherClientText.HandleCount}: {details.HandleCount}",
            $"{TeacherClientText.PriorityClass}: {ValueOrFallback(details.PriorityClass)}",
            $"{TeacherClientText.TotalProcessorTime}: {details.TotalProcessorTime}",
            $"{TeacherClientText.FileVersion}: {ValueOrFallback(details.FileVersion)}",
            $"{TeacherClientText.ProductName}: {ValueOrFallback(details.ProductName)}",
            $"{TeacherClientText.Error}: {ValueOrFallback(details.ErrorMessage)}"
        ]);
    }

    private static string ValueOrFallback(string? value)
        => string.IsNullOrWhiteSpace(value) ? TeacherClientText.NotAvailable : value;

    private static string FormatBool(bool value)
        => value ? TeacherClientText.Yes : TeacherClientText.No;

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
