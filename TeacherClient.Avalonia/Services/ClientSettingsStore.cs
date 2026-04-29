using System.Text.Json;
using System.Text.Json.Serialization;
using Teacher.Common;
using Teacher.Common.Localization;
using TeacherClient.CrossPlatform.Models;

namespace TeacherClient.CrossPlatform.Services;

public sealed class ClientSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly object _sync = new();
    private readonly string _storagePath;

    public ClientSettingsStore()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDirectory = string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(AppContext.BaseDirectory, "data")
            : Path.Combine(localAppData, "TeacherServer", "TeacherClient.Avalonia");

        Directory.CreateDirectory(baseDirectory);
        _storagePath = Path.Combine(baseDirectory, "settings.json");
    }

    public ClientSettings Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_storagePath))
            {
                return ClientSettings.Default;
            }

            try
            {
                var json = File.ReadAllText(_storagePath);
                var settings = JsonSerializer.Deserialize<ClientSettings>(json, JsonOptions);
                return Normalize(settings);
            }
            catch
            {
                return ClientSettings.Default;
            }
        }
    }

    public void Save(ClientSettings settings)
    {
        lock (_sync)
        {
            var json = JsonSerializer.Serialize(Normalize(settings), JsonOptions);

            File.WriteAllText(_storagePath, json);
        }
    }

    private static ClientSettings Normalize(ClientSettings? settings)
    {
        var sharedSecret = string.IsNullOrWhiteSpace(settings?.SharedSecret)
            ? ClientSettings.Default.SharedSecret
            : settings.SharedSecret.Trim();
        var language = settings?.Language.Normalize() ?? UiLanguageExtensions.GetDefault();
        var bulkCopyDestinationPath = string.IsNullOrWhiteSpace(settings?.BulkCopyDestinationPath)
            ? ClientSettings.Default.BulkCopyDestinationPath
            : RemoteWindowsPath.Normalize(settings.BulkCopyDestinationPath);
        var studentWorkRootPath = string.IsNullOrWhiteSpace(settings?.StudentWorkRootPath)
            ? ClientSettings.Default.StudentWorkRootPath
            : RemoteWindowsPath.Normalize(settings.StudentWorkRootPath);
        var studentWorkFolderName = string.IsNullOrWhiteSpace(settings?.StudentWorkFolderName)
            ? ClientSettings.Default.StudentWorkFolderName
            : settings.StudentWorkFolderName.Trim();
        var configuredDesktopIconAutoRestoreMinutes = settings?.DesktopIconAutoRestoreMinutes;
        var desktopIconAutoRestoreMinutes = configuredDesktopIconAutoRestoreMinutes <= 0
            ? ClientSettings.Default.DesktopIconAutoRestoreMinutes
            : configuredDesktopIconAutoRestoreMinutes.GetValueOrDefault();
        var configuredBrowserLockCheckIntervalSeconds = settings?.BrowserLockCheckIntervalSeconds;
        var browserLockCheckIntervalSeconds = configuredBrowserLockCheckIntervalSeconds <= 0
            ? ClientSettings.Default.BrowserLockCheckIntervalSeconds
            : Math.Max(5, configuredBrowserLockCheckIntervalSeconds.GetValueOrDefault());

        var theme = settings?.Theme ?? default;
        if (theme != AppUiTheme.Dark && theme != AppUiTheme.Light)
        {
            theme = ClientSettings.Default.Theme;
        }

        return new ClientSettings(
            sharedSecret,
            language,
            bulkCopyDestinationPath,
            studentWorkRootPath,
            studentWorkFolderName,
            Math.Max(1, desktopIconAutoRestoreMinutes),
            browserLockCheckIntervalSeconds,
            theme);
    }
}
