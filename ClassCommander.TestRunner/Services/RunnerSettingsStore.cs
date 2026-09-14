using System.Text.Json;
using ClassCommander.Testing.Core.Serialization;

namespace ClassCommander.TestRunner.Services;

internal sealed class RunnerSettingsStore
{
    private readonly string _path;

    public RunnerSettingsStore()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClassCommander",
            "TestRunner");
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "settings.json");
    }

    public RunnerSettings Load()
    {
        if (!File.Exists(_path))
        {
            return new RunnerSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<RunnerSettings>(File.ReadAllText(_path), TestPlatformJson.Options)
                ?? new RunnerSettings();
        }
        catch
        {
            return new RunnerSettings();
        }
    }

    public void Save(RunnerSettings settings)
    {
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, TestPlatformJson.Options));
    }
}

internal sealed class RunnerSettings
{
    public string ServerUrl { get; set; } = "http://127.0.0.1:5000";

    public string Surname { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    public string Language { get; set; } = "en";
}
