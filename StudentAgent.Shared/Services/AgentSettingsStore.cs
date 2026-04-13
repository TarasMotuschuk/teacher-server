using System.Security;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using Teacher.Common;
using Teacher.Common.Contracts;
using Teacher.Common.Localization;

namespace StudentAgent.Services;

public sealed class AgentSettingsStore
{
    private const string RegistryKeyPath = @"Software\TeacherServer\StudentAgent";
    private const string ValueNameSharedSecretProtected = "SharedSecretProtected";
    private const string ValueNameVncPasswordProtected = "VncPasswordProtected";

    private readonly object _sync = new();
    private readonly bool _useRegistry;
    private readonly bool _canWriteHklm;
    private readonly AgentOptions _defaults;
    private AgentRuntimeSettings _current;

    public AgentSettingsStore(IOptions<AgentOptions> options)
    {
        _defaults = options.Value;
        _useRegistry = OperatingSystem.IsWindows();
        _canWriteHklm = !_useRegistry || TryCanCreateMachineAgentRegistryKey();

        lock (_sync)
        {
            if (_useRegistry)
            {
                _current = LoadFromRegistry(_defaults);
                if (_canWriteHklm)
                {
                    PersistSettings(_current, _current.SharedSecret);
                }
            }
            else
            {
                _current = NewRuntimeFromAgentOptions(_defaults);
            }
        }
    }

    public event EventHandler? SettingsChanged;

    public AgentRuntimeSettings Current
    {
        get
        {
            EventHandler? changedHandler = null;
            var changed = false;
            lock (_sync)
            {
                changed = _useRegistry && ReloadFromRegistryIfChanged();
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

    /// <summary>
    /// Applies a full settings snapshot (used by the local HTTP API when session processes cannot write HKLM).
    /// </summary>
    /// <param name="snapshot">The full runtime settings snapshot to persist and broadcast.</param>
    public void ImportRuntimeSettings(AgentRuntimeSettings snapshot)
    {
        lock (_sync)
        {
            _current = Normalize(Clone(snapshot), _defaults);
            if (_useRegistry)
            {
                if (_canWriteHklm)
                {
                    WriteAllValuesToHklm(_current);
                }
            }
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
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

            var authorizationSecret = _current.SharedSecret;
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
                PersistSettings(_current, authorizationSecret);
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

            _current.DesktopIconAutoRestoreMinutes = Math.Max(1, desktopIconAutoRestoreMinutes);
            _current.BrowserLockCheckIntervalSeconds = Math.Max(5, browserLockCheckIntervalSeconds);
            if (_useRegistry)
            {
                PersistSettings(_current, _current.SharedSecret);
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

            _current.BrowserLockEnabled = enabled;
            if (_useRegistry)
            {
                PersistSettings(_current, _current.SharedSecret);
            }
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateInputLock(bool enabled, InputLockVisualMode? visualMode = null)
    {
        lock (_sync)
        {
            if (_useRegistry)
            {
                ReloadFromRegistryIfChanged();
            }

            _current.InputLockEnabled = enabled;
            if (!enabled)
            {
                _current.InputLockVisualMode = InputLockVisualMode.FullscreenOverlay;
            }
            else if (visualMode is not null)
            {
                _current.InputLockVisualMode = visualMode.Value;
            }

            if (_useRegistry)
            {
                PersistSettings(_current, _current.SharedSecret);
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
                PersistSettings(_current, _current.SharedSecret);
            }
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool TryCanCreateMachineAgentRegistryKey()
    {
        try
        {
            Registry.LocalMachine.CreateSubKey(RegistryKeyPath, writable: true)?.Dispose();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (SecurityException)
        {
            return false;
        }
    }

    private static AgentRuntimeSettings NewRuntimeFromAgentOptions(AgentOptions defaults)
    {
        return Normalize(
            new AgentRuntimeSettings
            {
                Port = defaults.Port,
                DiscoveryPort = defaults.DiscoveryPort,
                SharedSecret = defaults.SharedSecret,
                AdminPasswordHash = defaults.AdminPasswordHash,
                VisibleBannerText = defaults.VisibleBannerText,
                Language = UiLanguageExtensions.GetDefault(),
                BrowserLockEnabled = defaults.BrowserLockEnabled,
                InputLockEnabled = defaults.InputLockEnabled,
                InputLockVisualMode = defaults.InputLockVisualMode,
                BrowserLockCheckIntervalSeconds = defaults.BrowserLockCheckIntervalSeconds,
                DesktopIconAutoRestoreMinutes = defaults.DesktopIconAutoRestoreMinutes,
                VncEnabled = defaults.VncEnabled,
                VncPort = defaults.VncPort,
                VncViewOnly = defaults.VncViewOnly,
                VncPassword = string.IsNullOrWhiteSpace(defaults.VncPassword)
                    ? VncPasswordHelper.Derive(defaults.SharedSecret)
                    : defaults.VncPassword,
            },
            defaults);
    }

    private static AgentRuntimeSettings LoadFromRegistry(AgentOptions defaults)
    {
        using var key = Registry.LocalMachine.OpenSubKey(RegistryKeyPath, writable: false);
        if (key is null)
        {
            return NewRuntimeFromAgentOptions(defaults);
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
            InputLockVisualMode = ReadInputLockVisualMode(key, nameof(AgentRuntimeSettings.InputLockVisualMode), defaults.InputLockVisualMode),
            BrowserLockCheckIntervalSeconds = ReadInt(key, nameof(AgentRuntimeSettings.BrowserLockCheckIntervalSeconds), defaults.BrowserLockCheckIntervalSeconds),
            DesktopIconAutoRestoreMinutes = ReadInt(key, nameof(AgentRuntimeSettings.DesktopIconAutoRestoreMinutes), defaults.DesktopIconAutoRestoreMinutes),
            VncEnabled = ReadBool(key, nameof(AgentRuntimeSettings.VncEnabled), defaults.VncEnabled),
            VncPort = ReadInt(key, nameof(AgentRuntimeSettings.VncPort), defaults.VncPort),
            VncViewOnly = ReadBool(key, nameof(AgentRuntimeSettings.VncViewOnly), defaults.VncViewOnly),
            VncPassword = ReadProtectedString(key, ValueNameVncPasswordProtected)
                          ?? (string.IsNullOrWhiteSpace(defaults.VncPassword)
                              ? VncPasswordHelper.Derive(defaults.SharedSecret)
                              : defaults.VncPassword),
        };

        return Normalize(loaded, defaults);
    }

    private static void PushRuntimeSettingsToLocalAgent(AgentRuntimeSettings settings, string authorizationSharedSecret)
    {
        var url = $"http://127.0.0.1:{Math.Max(1, settings.Port)}/api/agent/runtime-settings";
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.TryAddWithoutValidation("X-Teacher-Secret", authorizationSharedSecret);
        request.Content = System.Net.Http.Json.JsonContent.Create(settings);
        using var response = client.Send(request);
        if (!response.IsSuccessStatusCode)
        {
            var detail = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(detail)
                    ? $"Local agent rejected settings update (HTTP {(int)response.StatusCode})."
                    : detail);
        }
    }

    private static void WriteAllValuesToHklm(AgentRuntimeSettings settings)
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
        key.SetValue(nameof(AgentRuntimeSettings.InputLockVisualMode), settings.InputLockVisualMode.ToString(), RegistryValueKind.String);
        key.SetValue(nameof(AgentRuntimeSettings.BrowserLockCheckIntervalSeconds), settings.BrowserLockCheckIntervalSeconds, RegistryValueKind.DWord);
        key.SetValue(nameof(AgentRuntimeSettings.DesktopIconAutoRestoreMinutes), settings.DesktopIconAutoRestoreMinutes, RegistryValueKind.DWord);
        key.SetValue(nameof(AgentRuntimeSettings.VncEnabled), settings.VncEnabled ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue(nameof(AgentRuntimeSettings.VncPort), settings.VncPort, RegistryValueKind.DWord);
        key.SetValue(nameof(AgentRuntimeSettings.VncViewOnly), settings.VncViewOnly ? 1 : 0, RegistryValueKind.DWord);
        WriteProtectedString(key, ValueNameVncPasswordProtected, settings.VncPassword ?? string.Empty);
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
        value.InputLockVisualMode = Enum.IsDefined(value.InputLockVisualMode) ? value.InputLockVisualMode : defaults.InputLockVisualMode;
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
            InputLockVisualMode = settings.InputLockVisualMode,
            BrowserLockCheckIntervalSeconds = settings.BrowserLockCheckIntervalSeconds,
            DesktopIconAutoRestoreMinutes = settings.DesktopIconAutoRestoreMinutes,
            VncEnabled = settings.VncEnabled,
            VncPort = settings.VncPort,
            VncViewOnly = settings.VncViewOnly,
            VncPassword = settings.VncPassword,
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
            && left.InputLockVisualMode == right.InputLockVisualMode
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

    private static int ReadInt(RegistryKey key, string name, int defaultValue)
    {
        try
        {
            var value = key.GetValue(name);
            return value switch
            {
                int i => i,
                string s when int.TryParse(s, out var parsed) => parsed,
                _ => defaultValue,
            };
        }
        catch
        {
            return defaultValue;
        }
    }

    private static bool ReadBool(RegistryKey key, string name, bool defaultValue)
        => ReadInt(key, name, defaultValue ? 1 : 0) != 0;

    private static InputLockVisualMode ReadInputLockVisualMode(RegistryKey key, string name, InputLockVisualMode defaultValue)
    {
        try
        {
            var raw = ReadString(key, name);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            if (Enum.TryParse<InputLockVisualMode>(raw, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            {
                return parsed;
            }

            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

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

    private void PersistSettings(AgentRuntimeSettings settings, string authorizationSharedSecretForHttp)
    {
        if (!_useRegistry)
        {
            return;
        }

        if (_canWriteHklm)
        {
            WriteAllValuesToHklm(settings);
            return;
        }

        PushRuntimeSettingsToLocalAgent(settings, authorizationSharedSecretForHttp);
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
}
