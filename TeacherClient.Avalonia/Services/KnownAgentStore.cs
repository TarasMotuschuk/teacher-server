using System.Text.Json;
using TeacherClient.CrossPlatform.Models;

namespace TeacherClient.CrossPlatform.Services;

public sealed class KnownAgentStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly TimeSpan Retention = TimeSpan.FromDays(180);

    private readonly object _sync = new();
    private readonly string _storagePath;

    public KnownAgentStore()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDirectory = string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(AppContext.BaseDirectory, "data")
            : Path.Combine(localAppData, "TeacherServer", "TeacherClient.Avalonia");

        Directory.CreateDirectory(baseDirectory);
        _storagePath = Path.Combine(baseDirectory, "known-agents.json");
    }

    public IReadOnlyList<KnownAgentEntry> Load()
    {
        lock (_sync)
        {
            return LoadUnlocked();
        }
    }

    public void Upsert(IEnumerable<DiscoveredAgentRow> agents)
    {
        lock (_sync)
        {
            var byId = LoadUnlocked().ToDictionary(x => x.AgentId, StringComparer.OrdinalIgnoreCase);
            foreach (var agent in agents)
            {
                if (string.IsNullOrWhiteSpace(agent.AgentId) || agent.IsManual)
                {
                    continue;
                }

                byId.TryGetValue(agent.AgentId, out var existing);
                var mac = string.IsNullOrWhiteSpace(agent.MacAddressesDisplay)
                    ? existing?.MacAddresses ?? string.Empty
                    : agent.MacAddressesDisplay.Trim();
                var lastSeen = agent.LastSeenUtc == DateTime.MinValue
                    ? existing?.LastSeenUtc ?? DateTime.UtcNow
                    : agent.LastSeenUtc;

                byId[agent.AgentId] = new KnownAgentEntry
                {
                    AgentId = agent.AgentId,
                    MachineName = string.IsNullOrWhiteSpace(agent.MachineName)
                        ? existing?.MachineName ?? agent.AgentId
                        : agent.MachineName,
                    RespondingAddress = string.IsNullOrWhiteSpace(agent.RespondingAddress)
                        ? existing?.RespondingAddress ?? string.Empty
                        : agent.RespondingAddress,
                    Port = agent.Port > 0 ? agent.Port : existing?.Port ?? 5055,
                    MacAddresses = mac,
                    LastSeenUtc = lastSeen,
                };
            }

            SaveUnlocked(byId.Values);
        }
    }

    private IReadOnlyList<KnownAgentEntry> LoadUnlocked()
    {
        if (!File.Exists(_storagePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_storagePath);
            var loaded = JsonSerializer.Deserialize<List<KnownAgentEntry>>(json) ?? [];
            var cutoff = DateTime.UtcNow - Retention;
            return loaded
                .Where(x => !string.IsNullOrWhiteSpace(x.AgentId) && x.LastSeenUtc >= cutoff)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private void SaveUnlocked(IEnumerable<KnownAgentEntry> entries)
    {
        var cutoff = DateTime.UtcNow - Retention;
        var cleaned = entries
            .Where(x => !string.IsNullOrWhiteSpace(x.AgentId) && x.LastSeenUtc >= cutoff)
            .OrderBy(x => x.MachineName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        File.WriteAllText(_storagePath, JsonSerializer.Serialize(cleaned, JsonOptions));
    }
}
