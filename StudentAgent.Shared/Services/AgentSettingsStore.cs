using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Teacher.Common.Localization;

namespace StudentAgent.Services;

public sealed class AgentSettingsStore
{
    private readonly object _sync = new();
    private readonly string _settingsPath;
    private readonly AgentOptions _defaults;
    private AgentRuntimeSettings _current;
    public event EventHandler? SettingsChanged;

    public AgentSettingsStore(IOptions<AgentOptions> options)
    {
        _defaults = options.Value;
        var dataDirectory = GetDataDirectory();
        Directory.CreateDirectory(dataDirectory);
        _settingsPath = Path.Combine(dataDirectory, "agentsettings.json");
        _current = Load(_defaults);
        Save(_current);
    }

    public AgentRuntimeSettings Current
    {
        get
        {
            EventHandler? changedHandler = null;
            var changed = false;
            lock (_sync)
            {
                changed = ReloadFromDiskIfChanged();
                if (changed)
                {
                    changedHandler = SettingsChanged;
                }

                var snapshot = Clone(_current);
                if (changed && changedHandler is not null)
                {
                    Task.Run(() => changedHandler.Invoke(this, EventArgs.Empty));
                }

                return snapshot;
            }
        }
    }

    public bool VerifyPassword(string password)
    {
        var hash = HashPassword(password);
        lock (_sync)
        {
            ReloadFromDiskIfChanged();
            return string.Equals(hash, _current.AdminPasswordHash, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void UpdateSettings(string sharedSecret, string? password, UiLanguage language)
    {
        lock (_sync)
        {
            ReloadFromDiskIfChanged();
            _current.SharedSecret = string.IsNullOrWhiteSpace(sharedSecret) ? _current.SharedSecret : sharedSecret.Trim();
            _current.Language = language;
            if (!string.IsNullOrWhiteSpace(password))
            {
                _current.AdminPasswordHash = HashPassword(password.Trim());
            }

            Save(_current);
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdatePolicySettings(int desktopIconAutoRestoreMinutes, int browserLockCheckIntervalSeconds)
    {
        lock (_sync)
        {
            ReloadFromDiskIfChanged();
            _current.DesktopIconAutoRestoreMinutes = Math.Max(1, desktopIconAutoRestoreMinutes);
            _current.BrowserLockCheckIntervalSeconds = Math.Max(5, browserLockCheckIntervalSeconds);
            Save(_current);
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateBrowserLock(bool enabled)
    {
        lock (_sync)
        {
            ReloadFromDiskIfChanged();
            _current.BrowserLockEnabled = enabled;
            Save(_current);
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateInputLock(bool enabled)
    {
        lock (_sync)
        {
            ReloadFromDiskIfChanged();
            _current.InputLockEnabled = enabled;
            Save(_current);
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool ReloadFromDiskIfChanged()
    {
        if (!File.Exists(_settingsPath))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var loaded = JsonSerializer.Deserialize<AgentRuntimeSettings>(json);
            if (loaded is null)
            {
                return false;
            }

            var normalized = Normalize(loaded, _defaults);
            if (AreEquivalent(_current, normalized))
            {
                return false;
            }

            _current = normalized;
            return true;
        }
        catch
        {
            return false;
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
            VisibleBannerText = defaults.VisibleBannerText,
            Language = UiLanguageExtensions.GetDefault(),
            BrowserLockEnabled = defaults.BrowserLockEnabled,
            InputLockEnabled = defaults.InputLockEnabled,
            BrowserLockCheckIntervalSeconds = defaults.BrowserLockCheckIntervalSeconds,
            DesktopIconAutoRestoreMinutes = defaults.DesktopIconAutoRestoreMinutes
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
        value.Language = value.Language.Normalize();
        value.BrowserLockEnabled = value.BrowserLockEnabled;
        value.InputLockEnabled = value.InputLockEnabled;
        value.BrowserLockCheckIntervalSeconds = value.BrowserLockCheckIntervalSeconds <= 0
            ? Math.Max(5, defaults.BrowserLockCheckIntervalSeconds)
            : Math.Max(5, value.BrowserLockCheckIntervalSeconds);
        value.DesktopIconAutoRestoreMinutes = value.DesktopIconAutoRestoreMinutes <= 0
            ? Math.Max(1, defaults.DesktopIconAutoRestoreMinutes)
            : value.DesktopIconAutoRestoreMinutes;
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
            VisibleBannerText = settings.VisibleBannerText,
            Language = settings.Language,
            BrowserLockEnabled = settings.BrowserLockEnabled,
            InputLockEnabled = settings.InputLockEnabled,
            BrowserLockCheckIntervalSeconds = settings.BrowserLockCheckIntervalSeconds,
            DesktopIconAutoRestoreMinutes = settings.DesktopIconAutoRestoreMinutes
        };
    }

    private static bool AreEquivalent(AgentRuntimeSettings left, AgentRuntimeSettings right)
    {
        return left.Port == right.Port
            && left.DiscoveryPort == right.DiscoveryPort
            && string.Equals(left.SharedSecret, right.SharedSecret, StringComparison.Ordinal)
            && string.Equals(left.AdminPasswordHash, right.AdminPasswordHash, StringComparison.Ordinal)
            && string.Equals(left.VisibleBannerText, right.VisibleBannerText, StringComparison.Ordinal)
            && left.Language == right.Language
            && left.BrowserLockEnabled == right.BrowserLockEnabled
            && left.InputLockEnabled == right.InputLockEnabled
            && left.BrowserLockCheckIntervalSeconds == right.BrowserLockCheckIntervalSeconds
            && left.DesktopIconAutoRestoreMinutes == right.DesktopIconAutoRestoreMinutes;
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GetDataDirectory()
        => StudentAgentPathHelper.GetRootDirectory();
}
