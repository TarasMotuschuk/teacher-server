using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace StudentAgent.Services;

public sealed class AgentSettingsStore
{
    private readonly object _sync = new();
    private readonly string _settingsPath;
    private AgentRuntimeSettings _current;

    public AgentSettingsStore(IOptions<AgentOptions> options)
    {
        var dataDirectory = GetDataDirectory();
        Directory.CreateDirectory(dataDirectory);
        _settingsPath = Path.Combine(dataDirectory, "agentsettings.json");
        _current = Load(options.Value);
        Save(_current);
    }

    public AgentRuntimeSettings Current
    {
        get
        {
            lock (_sync)
            {
                return Clone(_current);
            }
        }
    }

    public bool VerifyPassword(string password)
    {
        var hash = HashPassword(password);
        lock (_sync)
        {
            return string.Equals(hash, _current.AdminPasswordHash, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void UpdateCredentials(string sharedSecret, string? password)
    {
        lock (_sync)
        {
            _current.SharedSecret = string.IsNullOrWhiteSpace(sharedSecret) ? _current.SharedSecret : sharedSecret.Trim();
            if (!string.IsNullOrWhiteSpace(password))
            {
                _current.AdminPasswordHash = HashPassword(password.Trim());
            }

            Save(_current);
        }
    }

    private AgentRuntimeSettings Load(AgentOptions defaults)
    {
        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = File.ReadAllText(_settingsPath);
                var loaded = JsonSerializer.Deserialize<AgentRuntimeSettings>(json);
                if (loaded is not null)
                {
                    return Normalize(loaded, defaults);
                }
            }
            catch
            {
            }
        }

        return Normalize(new AgentRuntimeSettings
        {
            Port = defaults.Port,
            DiscoveryPort = defaults.DiscoveryPort,
            SharedSecret = defaults.SharedSecret,
            AdminPasswordHash = defaults.AdminPasswordHash,
            VisibleBannerText = defaults.VisibleBannerText
        }, defaults);
    }

    private void Save(AgentRuntimeSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_settingsPath, json);
    }

    private static AgentRuntimeSettings Normalize(AgentRuntimeSettings value, AgentOptions defaults)
    {
        value.Port = value.Port <= 0 ? defaults.Port : value.Port;
        value.DiscoveryPort = value.DiscoveryPort <= 0 ? defaults.DiscoveryPort : value.DiscoveryPort;
        value.SharedSecret = string.IsNullOrWhiteSpace(value.SharedSecret) ? defaults.SharedSecret : value.SharedSecret.Trim();
        value.AdminPasswordHash = string.IsNullOrWhiteSpace(value.AdminPasswordHash) ? defaults.AdminPasswordHash : value.AdminPasswordHash.Trim();
        value.VisibleBannerText = string.IsNullOrWhiteSpace(value.VisibleBannerText) ? defaults.VisibleBannerText : value.VisibleBannerText.Trim();
        return value;
    }

    private static AgentRuntimeSettings Clone(AgentRuntimeSettings settings)
    {
        return new AgentRuntimeSettings
        {
            Port = settings.Port,
            DiscoveryPort = settings.DiscoveryPort,
            SharedSecret = settings.SharedSecret,
            AdminPasswordHash = settings.AdminPasswordHash,
            VisibleBannerText = settings.VisibleBannerText
        };
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GetDataDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            return Path.Combine(AppContext.BaseDirectory, "data");
        }

        return Path.Combine(localAppData, "TeacherServer", "StudentAgent");
    }
}
