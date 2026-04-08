using Teacher.Common.Vnc;
using TeacherClient.CrossPlatform.Localization;

namespace TeacherClient.CrossPlatform;

internal static class RemoteManagementViewHelpers
{
    internal static string BuildRemoteManagementStatusText(
        string status,
        string machineName,
        bool vncEnabled,
        bool vncRunning,
        bool vncViewOnly,
        string? vncStatusMessage)
    {
        var baseStatus = !string.Equals(status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase)
            ? CrossPlatformText.RemoteManagementStopped(machineName)
            : !vncEnabled
                ? CrossPlatformText.RemoteManagementDisabled(machineName)
                : vncRunning
                    ? (vncViewOnly
                        ? CrossPlatformText.RemoteManagementViewOnly(machineName)
                        : CrossPlatformText.RemoteManagementControl(machineName))
                    : CrossPlatformText.RemoteManagementStopped(machineName);

        return string.IsNullOrWhiteSpace(vncStatusMessage)
            ? baseStatus
            : $"{baseStatus} - {vncStatusMessage}";
    }

    internal static void StopRemoteManagementPreviewNoWait(MainWindow.RemoteManagementTileViewModel tile)
    {
        tile.PreviewCancellation?.Cancel();
        tile.Session = null;
        tile.PreviewCancellation = null;
        tile.PreviewTask = null;
    }

    internal static async Task StopRemoteManagementPreviewAsync(MainWindow.RemoteManagementTileViewModel tile)
    {
        var wait = tile.PreviewTask;
        tile.PreviewCancellation?.Cancel();
        tile.Session = null;
        tile.PreviewCancellation = null;
        tile.PreviewTask = null;
        if (wait is not null)
        {
            try
            {
                await wait.ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    internal static MainWindow.PinnedPreviewBitmap CreatePreviewBitmap(VncFrameCapture frame)
        => MainWindow.PinnedPreviewBitmap.Create(frame.Pixels, frame.Width, frame.Height, frame.Stride);

    internal static MainWindow.PinnedPreviewBitmap CreatePlaceholderBitmap(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 36;
            pixels[index + 1] = 29;
            pixels[index + 2] = 24;
            pixels[index + 3] = 255;
        }

        return MainWindow.PinnedPreviewBitmap.Create(pixels, width, height, width * 4);
    }
}
