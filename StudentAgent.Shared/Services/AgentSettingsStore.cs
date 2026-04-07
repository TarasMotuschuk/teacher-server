using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using Microsoft.Extensions.Options;
using Teacher.Common;
using Teacher.Common.Localization;

namespace StudentAgent.Services;

public sealed class AgentSettingsStore
{
    private readonly object _sync = new();
    private readonly bool _useRegistry;
    private readonly string _legacySettingsPath;
    private readonly AgentOptions _defaults;
    private AgentRuntimeSettings _current;
    public event EventHandler? SettingsChanged;

    public AgentSettingsStore(IOptions<AgentOptions> options)
    {
        _defaults = options.Value;
        _useRegistry = OperatingSystem.IsWindows();
        _legacySettingsPath = Path.Combine(GetDataDirectory(), "agentsettings.json");

        lock (_sync)
        {
            if (_useRegistry)
            {
                TryMigrateLegacyJsonToRegistry();
                _current = LoadFromRegistry(_defaults);
                SaveToRegistry(_current);
            }
            else
            {
                var dataDirectory = GetDataDirectory();
                Directory.CreateDirectory(dataDirectory);
                _current = LoadFromDisk(_defaults);
                SaveToDisk(_current);
            }
        }
    }

    public AgentRuntimeSettings Current
    {
        get
        {
            EventHandler? changedHandler = null;
            var changed = false;
            lock (_sync)
            {
                changed = _useRegistry ? ReloadFromRegistryIfChanged() : ReloadFromDiskIfChanged();
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
            if (_useRegistry)
            {
                ReloadFromRegistryIfChanged();
            }
            else
            {
                ReloadFromDiskIfChanged();
            }
            return string.Equals(hash, _current.AdminPasswordHash, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void UpdateSettings(string sharedSecret, string? password, UiLanguage language)
    {
        lock (_sync)
        {
            if (_useRegistry)
            {
                ReloadFromRegistryIfChanged();
            }
            else
            {
                ReloadFromDiskIfChanged();
            }
            var previousSharedSecret = _current.SharedSecret;
            var nextSharedSecret = string.IsNullOrWhiteSpace(sharedSecret) ? _current.SharedSecret : sharedSecret.Trim();
            var shouldRefreshDerivedVncPassword = string.IsNullOrWhiteSpace(_current.VncPassword)
                || string.Equals(_current.VncPassword, VncPasswordHelper.Derive(previousSharedSecret), StringComparison.Ordinal);

            _current.SharedSecret = nextSharedSecret;
            _current.Language = language;
            if (!string.IsNullOrWhiteSpace(password))
            {
                _current.AdminPasswordHash = HashPassword(password.Trim());
            }

            if (shouldRefreshDerivedVncPassword)
            {
                _current.VncPassword = VncPasswordHelper.Derive(_current.SharedSecret);
            }

            if (_useRegistry)
            {
                SaveToRegistry(_current);
            }
            else
            {
                SaveToDisk(_current);
            }
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdatePolicySettings(int desktopIconAutoRestoreMinutes, int browserLockCheckIntervalSeconds)
    {
        lock (_sync)
        {
            if (_useRegistry)
            {
                ReloadFromRegistryIfChanged();
            }
            else
            {
                ReloadFromDiskIfChanged();
            }
            _current.DesktopIconAutoRestoreMinutes = Math.Max(1, desktopIconAutoRestoreMinutes);
            _current.BrowserLockCheckIntervalSeconds = Math.Max(5, browserLockCheckIntervalSeconds);
            if (_useRegistry)
            {
                SaveToRegistry(_current);
            }
            else
            {
                SaveToDisk(_current);
            }
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateBrowserLock(bool enabled)
    {
        lock (_sync)
        {
            if (_useRegistry)
            {
                ReloadFromRegistryIfChanged();
            }
            else
            {
                ReloadFromDiskIfChanged();
            }
            _current.BrowserLockEnabled = enabled;
            if (_useRegistry)
            {
                SaveToRegistry(_current);
            }
            else
            {
                SaveToDisk(_current);
            }
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateInputLock(bool enabled)
    {
        lock (_sync)
        {
            if (_useRegistry)
            {
                ReloadFromRegistryIfChanged();
            }
            else
            {
                ReloadFromDiskIfChanged();
            }
            _current.InputLockEnabled = enabled;
            if (_useRegistry)
            {
                SaveToRegistry(_current);
            }
            else
            {
                SaveToDisk(_current);
            }
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateVncSettings(bool enabled, int? port = null, bool? viewOnly = null, string? password = null)
    {
        lock (_sync)
        {
            if (_useRegistry)
            {
                ReloadFromRegistryIfChanged();
            }
            else
            {
                ReloadFromDiskIfChanged();
            }
            _current.VncEnabled = enabled;
            if (port is not null)
            {
                _current.VncPort = Math.Max(1, port.GetValueOrDefault());
            }

            if (viewOnly is not null)
            {
                _current.VncViewOnly = viewOnly.GetValueOrDefault();
            }

            if (!string.IsNullOrWhiteSpace(password))
            {
                _current.VncPassword = password.Trim();
            }
            else if (string.IsNullOrWhiteSpace(_current.VncPassword))
            {
                _current.VncPassword = VncPasswordHelper.Derive(_current.SharedSecret);
            }

            if (_useRegistry)
            {
                SaveToRegistry(_current);
            }
            else
            {
                SaveToDisk(_current);
            }
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool ReloadFromRegistryIfChanged()
    {
        try
        {
            var loaded = LoadFromRegistry(_defaults);
            if (AreEquivalent(_current, loaded))
            {
                return false;
            }

            _current = loaded;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool ReloadFromDiskIfChanged()
    {
        if (!File.Exists(_legacySettingsPath))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(_legacySettingsPath);
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

    private AgentRuntimeSettings LoadFromDisk(AgentOptions defaults)
    {
        if (File.Exists(_legacySettingsPath))
        {
            try
            {
                var json = File.ReadAllText(_legacySettingsPath);
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
            DesktopIconAutoRestoreMinutes = defaults.DesktopIconAutoRestoreMinutes,
            VncEnabled = defaults.VncEnabled,
            VncPort = defaults.VncPort,
            VncViewOnly = defaults.VncViewOnly,
            VncPassword = string.IsNullOrWhiteSpace(defaults.VncPassword)
                ? VncPasswordHelper.Derive(defaults.SharedSecret)
                : defaults.VncPassword
        }, defaults);
    }

    private void SaveToDisk(AgentRuntimeSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_legacySettingsPath, json);
    }

    private AgentRuntimeSettings LoadFromRegistry(AgentOptions defaults)
    {
        using var key = Registry.LocalMachine.OpenSubKey(RegistryKeyPath, writable: false);
        if (key is null)
        {
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
                DesktopIconAutoRestoreMinutes = defaults.DesktopIconAutoRestoreMinutes,
                VncEnabled = defaults.VncEnabled,
                VncPort = defaults.VncPort,
                VncViewOnly = defaults.VncViewOnly,
                VncPassword = string.IsNullOrWhiteSpace(defaults.VncPassword)
                    ? VncPasswordHelper.Derive(defaults.SharedSecret)
                    : defaults.VncPassword
            }, defaults);
        }

        var loaded = new AgentRuntimeSettings
        {
            Port = ReadInt(key, nameof(AgentRuntimeSettings.Port), defaults.Port),
            DiscoveryPort = ReadInt(key, nameof(AgentRuntimeSettings.DiscoveryPort), defaults.DiscoveryPort),
            SharedSecret = ReadProtectedString(key, ValueNameSharedSecretProtected) ?? defaults.SharedSecret,
            AdminPasswordHash = ReadString(key, nameof(AgentRuntimeSettings.AdminPasswordHash)) ?? defaults.AdminPasswordHash,
            VisibleBannerText = ReadString(key, nameof(AgentRuntimeSettings.VisibleBannerText)) ?? defaults.VisibleBannerText,
            Language = ReadUiLanguage(key, nameof(AgentRuntimeSettings.Language), UiLanguageExtensions.GetDefault()),
            BrowserLockEnabled = ReadBool(key, nameof(AgentRuntimeSettings.BrowserLockEnabled), defaults.BrowserLockEnabled),
            InputLockEnabled = ReadBool(key, nameof(AgentRuntimeSettings.InputLockEnabled), defaults.InputLockEnabled),
            BrowserLockCheckIntervalSeconds = ReadInt(key, nameof(AgentRuntimeSettings.BrowserLockCheckIntervalSeconds), defaults.BrowserLockCheckIntervalSeconds),
            DesktopIconAutoRestoreMinutes = ReadInt(key, nameof(AgentRuntimeSettings.DesktopIconAutoRestoreMinutes), defaults.DesktopIconAutoRestoreMinutes),
            VncEnabled = ReadBool(key, nameof(AgentRuntimeSettings.VncEnabled), defaults.VncEnabled),
            VncPort = ReadInt(key, nameof(AgentRuntimeSettings.VncPort), defaults.VncPort),
            VncViewOnly = ReadBool(key, nameof(AgentRuntimeSettings.VncViewOnly), defaults.VncViewOnly),
            VncPassword = ReadProtectedString(key, ValueNameVncPasswordProtected)
                          ?? (string.IsNullOrWhiteSpace(defaults.VncPassword)
                              ? VncPasswordHelper.Derive(defaults.SharedSecret)
                              : defaults.VncPassword)
        };

        return Normalize(loaded, defaults);
    }

    private void SaveToRegistry(AgentRuntimeSettings settings)
    {
        using var key = Registry.LocalMachine.CreateSubKey(RegistryKeyPath, writable: true);
        if (key is null)
        {
            return;
        }

        key.SetValue(nameof(AgentRuntimeSettings.Port), settings.Port, RegistryValueKind.DWord);
        key.SetValue(nameof(AgentRuntimeSettings.DiscoveryPort), settings.DiscoveryPort, RegistryValueKind.DWord);
        WriteProtectedString(key, ValueNameSharedSecretProtected, settings.SharedSecret);
        key.SetValue(nameof(AgentRuntimeSettings.AdminPasswordHash), settings.AdminPasswordHash ?? string.Empty, RegistryValueKind.String);
        key.SetValue(nameof(AgentRuntimeSettings.VisibleBannerText), settings.VisibleBannerText ?? string.Empty, RegistryValueKind.String);
        key.SetValue(nameof(AgentRuntimeSettings.Language), settings.Language.ToString(), RegistryValueKind.String);
        key.SetValue(nameof(AgentRuntimeSettings.BrowserLockEnabled), settings.BrowserLockEnabled ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue(nameof(AgentRuntimeSettings.InputLockEnabled), settings.InputLockEnabled ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue(nameof(AgentRuntimeSettings.BrowserLockCheckIntervalSeconds), settings.BrowserLockCheckIntervalSeconds, RegistryValueKind.DWord);
        key.SetValue(nameof(AgentRuntimeSettings.DesktopIconAutoRestoreMinutes), settings.DesktopIconAutoRestoreMinutes, RegistryValueKind.DWord);
        key.SetValue(nameof(AgentRuntimeSettings.VncEnabled), settings.VncEnabled ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue(nameof(AgentRuntimeSettings.VncPort), settings.VncPort, RegistryValueKind.DWord);
        key.SetValue(nameof(AgentRuntimeSettings.VncViewOnly), settings.VncViewOnly ? 1 : 0, RegistryValueKind.DWord);
        WriteProtectedString(key, ValueNameVncPasswordProtected, settings.VncPassword ?? string.Empty);
        key.SetValue(ValueNameLegacyJsonMigrated, 1, RegistryValueKind.DWord);
    }

    private void TryMigrateLegacyJsonToRegistry()
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(RegistryKeyPath, writable: true);
            if (key is null)
            {
                return;
            }

            if (ReadInt(key, ValueNameLegacyJsonMigrated, 0) == 1)
            {
                return;
            }

            if (!File.Exists(_legacySettingsPath))
            {
                return;
            }

            AgentRuntimeSettings? loaded;
            try
            {
                var json = File.ReadAllText(_legacySettingsPath);
                loaded = JsonSerializer.Deserialize<AgentRuntimeSettings>(json);
            }
            catch
            {
                loaded = null;
            }

            if (loaded is null)
            {
                return;
            }

            var normalized = Normalize(loaded, _defaults);
            SaveToRegistry(normalized);

            TryRenameLegacyJson();
        }
        catch
        {
            // Best-effort migration: keep running with defaults/registry even if migration fails.
        }
    }

    private void TryRenameLegacyJson()
    {
        try
        {
            var migratedPath = $"{_legacySettingsPath}.migrated";
            if (File.Exists(migratedPath))
            {
                return;
            }

            File.Move(_legacySettingsPath, migratedPath);
        }
        catch
        {
        }
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
        value.VncEnabled = value.VncEnabled;
        value.VncPort = value.VncPort <= 0 ? Math.Max(1, defaults.VncPort) : Math.Max(1, value.VncPort);
        value.VncViewOnly = value.VncViewOnly;
        value.VncPassword = string.IsNullOrWhiteSpace(value.VncPassword)
            ? (string.IsNullOrWhiteSpace(defaults.VncPassword) ? VncPasswordHelper.Derive(value.SharedSecret) : defaults.VncPassword)
            : value.VncPassword.Trim();
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
            DesktopIconAutoRestoreMinutes = settings.DesktopIconAutoRestoreMinutes,
            VncEnabled = settings.VncEnabled,
            VncPort = settings.VncPort,
            VncViewOnly = settings.VncViewOnly,
            VncPassword = settings.VncPassword
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
            && left.DesktopIconAutoRestoreMinutes == right.DesktopIconAutoRestoreMinutes
            && left.VncEnabled == right.VncEnabled
            && left.VncPort == right.VncPort
            && left.VncViewOnly == right.VncViewOnly
            && string.Equals(left.VncPassword, right.VncPassword, StringComparison.Ordinal);
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GetDataDirectory()
        => StudentAgentPathHelper.GetRootDirectory();

    private const string RegistryKeyPath = @"Software\TeacherServer\StudentAgent";
    private const string ValueNameLegacyJsonMigrated = "LegacyJsonMigrated";
    private const string ValueNameSharedSecretProtected = "SharedSecretProtected";
    private const string ValueNameVncPasswordProtected = "VncPasswordProtected";

    private static int ReadInt(RegistryKey key, string name, int defaultValue)
    {
        try
        {
            var value = key.GetValue(name);
            return value switch
            {
                int i => i,
                string s when int.TryParse(s, out var parsed) => parsed,
                _ => defaultValue
            };
        }
        catch
        {
            return defaultValue;
        }
    }

    private static bool ReadBool(RegistryKey key, string name, bool defaultValue)
        => ReadInt(key, name, defaultValue ? 1 : 0) != 0;

    private static string? ReadString(RegistryKey key, string name)
    {
        try
        {
            return key.GetValue(name) as string;
        }
        catch
        {
            return null;
        }
    }

    private static UiLanguage ReadUiLanguage(RegistryKey key, string name, UiLanguage defaultValue)
    {
        try
        {
            var raw = ReadString(key, name);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            if (Enum.TryParse<UiLanguage>(raw, ignoreCase: true, out var parsed))
            {
                return parsed.Normalize();
            }

            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private static string? ReadProtectedString(RegistryKey key, string name)
    {
        try
        {
            var raw = key.GetValue(name);
            if (raw is not byte[] protectedBytes || protectedBytes.Length == 0)
            {
                return null;
            }

            var unprotected = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, scope: DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(unprotected);
        }
        catch
        {
            return null;
        }
    }

    private static void WriteProtectedString(RegistryKey key, string name, string value)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, scope: DataProtectionScope.LocalMachine);
            key.SetValue(name, protectedBytes, RegistryValueKind.Binary);
        }
        catch
        {
        }
    }
}
