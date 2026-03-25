using System.Text.Json;
using TeacherClient.Models;

namespace TeacherClient.Services;

public sealed class ManualAgentStore
{
    private readonly object _sync = new();
    private readonly string _storagePath;

    public ManualAgentStore()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDirectory = string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(AppContext.BaseDirectory, "data")
            : Path.Combine(localAppData, "TeacherServer", "TeacherClient");

        Directory.CreateDirectory(baseDirectory);
        _storagePath = Path.Combine(baseDirectory, "manual-agents.json");
    }

    public IReadOnlyList<ManualAgentEntry> Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_storagePath))
            {
                return [];
            }

            try
            {
                var json = File.ReadAllText(_storagePath);
                return JsonSerializer.Deserialize<List<ManualAgentEntry>>(json) ?? [];
            }
            catch
            {
                return [];
            }
        }
    }

    public void Save(IEnumerable<ManualAgentEntry> entries)
    {
        lock (_sync)
        {
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_storagePath, json);
        }
    }
}
