using Teacher.Common;

namespace TeacherClient.CrossPlatform.Dialogs;

internal static class UpdatePreparationWindowTextFormatter
{
    internal static string BuildProgressDetails(TeacherUpdatePreparationProgress progress)
    {
        if (progress.TotalBytes.HasValue && progress.BytesTransferred.HasValue)
        {
            var percent = progress.Percent is >= 0 and <= 100 ? $" ({progress.Percent.Value}%)" : string.Empty;
            return $"{FormatByteSize(progress.BytesTransferred.Value)} / {FormatByteSize(progress.TotalBytes.Value)}{percent}";
        }

        return progress.Percent is >= 0 and <= 100
            ? $"{progress.Percent.Value}%"
            : string.Empty;
    }

    internal static string BuildLogMessage(TeacherUpdatePreparationProgress progress)
    {
        var details = BuildProgressDetails(progress);
        return string.IsNullOrWhiteSpace(details) ? progress.Message : $"{progress.Message} {details}";
    }

    private static string FormatByteSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{value:0} {units[unitIndex]}" : $"{value:0.##} {units[unitIndex]}";
    }
}
