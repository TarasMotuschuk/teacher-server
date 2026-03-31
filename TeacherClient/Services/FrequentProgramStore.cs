using System.Text.Json;
using Teacher.Common.Contracts;
using TeacherClient.Models;

namespace TeacherClient.Services;

public sealed class FrequentProgramStore
{
    private readonly object _sync = new();
    private readonly string _storagePath;

    public FrequentProgramStore()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDirectory = string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(AppContext.BaseDirectory, "data")
            : Path.Combine(localAppData, "TeacherServer", "TeacherClient");

        Directory.CreateDirectory(baseDirectory);
        _storagePath = Path.Combine(baseDirectory, "frequent-programs.json");
    }

    public IReadOnlyList<FrequentProgramEntry> Load()
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
                var items = JsonSerializer.Deserialize<List<FrequentProgramEntry>>(json);
                return Normalize(items);
            }
            catch
            {
                return [];
            }
        }
    }

    public void Save(IEnumerable<FrequentProgramEntry> entries)
    {
        lock (_sync)
        {
            var json = JsonSerializer.Serialize(Normalize(entries?.ToList()), new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_storagePath, json);
        }
    }

    private static List<FrequentProgramEntry> Normalize(IReadOnlyList<FrequentProgramEntry>? entries)
    {
        return (entries ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.DisplayName) && !string.IsNullOrWhiteSpace(x.CommandText))
            .Select(x => new FrequentProgramEntry(
                string.IsNullOrWhiteSpace(x.Id) ? Guid.NewGuid().ToString("N") : x.Id,
                x.DisplayName.Trim(),
                x.CommandText.Trim(),
                Enum.IsDefined(typeof(RemoteCommandRunAs), x.RunAs) ? x.RunAs : RemoteCommandRunAs.CurrentUser))
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.CommandText, StringComparer.OrdinalIgnoreCase)
            .DistinctBy(x => $"{x.DisplayName}|{x.CommandText}|{x.RunAs}", StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
