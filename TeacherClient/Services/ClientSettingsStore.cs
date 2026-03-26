using System.Text.Json;
using Teacher.Common.Localization;
using TeacherClient.Models;

namespace TeacherClient.Services;

public sealed class ClientSettingsStore
{
    private readonly object _sync = new();
    private readonly string _storagePath;

    public ClientSettingsStore()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDirectory = string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(AppContext.BaseDirectory, "data")
            : Path.Combine(localAppData, "TeacherServer", "TeacherClient");

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
                var settings = JsonSerializer.Deserialize<ClientSettings>(json);
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
            var json = JsonSerializer.Serialize(Normalize(settings), new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_storagePath, json);
        }
    }

    private static ClientSettings Normalize(ClientSettings? settings)
    {
        var sharedSecret = string.IsNullOrWhiteSpace(settings?.SharedSecret)
            ? ClientSettings.Default.SharedSecret
            : settings.SharedSecret.Trim();
        var language = settings?.Language.Normalize() ?? UiLanguageExtensions.GetDefault();

        return new ClientSettings(sharedSecret, language);
    }
}
