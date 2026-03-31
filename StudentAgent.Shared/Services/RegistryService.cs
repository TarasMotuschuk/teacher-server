using Microsoft.Win32;
using Teacher.Common.Contracts;

namespace StudentAgent.Services;

public sealed class RegistryService
{
    private static readonly string[] RootHives =
    [
        "HKEY_LOCAL_MACHINE",
        "HKEY_CURRENT_USER",
        "HKEY_CLASSES_ROOT",
        "HKEY_USERS",
        "HKEY_CURRENT_CONFIG"
    ];

    public IReadOnlyList<RegistryKeyDto> GetSubKeys(string path)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();

        if (string.IsNullOrWhiteSpace(path))
            return [.. RootHives.Select(h => new RegistryKeyDto(h, h, true))];

        var (hiveName, subPath) = SplitPath(path);
        var hive = OpenRootHive(hiveName);
        if (hive is null) return [];

        if (string.IsNullOrEmpty(subPath))
            return ListSubKeys(hive, hiveName);

        using var key = hive.OpenSubKey(subPath);
        return key is null ? [] : ListSubKeys(key, path);
    }

    public IReadOnlyList<RegistryValueDto> GetValues(string path)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();

        if (string.IsNullOrWhiteSpace(path)) return [];

        var (hiveName, subPath) = SplitPath(path);
        var hive = OpenRootHive(hiveName);
        if (hive is null) return [];

        if (string.IsNullOrEmpty(subPath))
            return ListValues(hive);

        using var key = hive.OpenSubKey(subPath);
        return key is null ? [] : ListValues(key);
    }

    private static IReadOnlyList<RegistryKeyDto> ListSubKeys(RegistryKey key, string pathPrefix)
    {
        return [.. key.GetSubKeyNames().Select(name =>
        {
            var childPath = $"{pathPrefix}\\{name}";
            bool hasChildren;
            try
            {
                using var child = key.OpenSubKey(name);
                hasChildren = child?.SubKeyCount > 0;
            }
            catch
            {
                hasChildren = false;
            }
            return new RegistryKeyDto(name, childPath, hasChildren);
        })];
    }

    private static IReadOnlyList<RegistryValueDto> ListValues(RegistryKey key)
    {
        return [.. key.GetValueNames().Select(name =>
        {
            var kind = key.GetValueKind(name);
            var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            return new RegistryValueDto(
                string.IsNullOrEmpty(name) ? "(Default)" : name,
                FormatKind(kind),
                FormatValue(value, kind));
        })];
    }

    private static (string hiveName, string subPath) SplitPath(string path)
    {
        var idx = path.IndexOf('\\');
        return idx < 0
            ? (path, string.Empty)
            : (path[..idx], path[(idx + 1)..]);
    }

    private static RegistryKey? OpenRootHive(string hive) => hive.ToUpperInvariant() switch
    {
        "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
        "HKEY_CURRENT_USER" => Registry.CurrentUser,
        "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
        "HKEY_USERS" => Registry.Users,
        "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
        _ => null
    };

    private static string FormatKind(RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.String => "REG_SZ",
        RegistryValueKind.ExpandString => "REG_EXPAND_SZ",
        RegistryValueKind.Binary => "REG_BINARY",
        RegistryValueKind.DWord => "REG_DWORD",
        RegistryValueKind.MultiString => "REG_MULTI_SZ",
        RegistryValueKind.QWord => "REG_QWORD",
        RegistryValueKind.None => "REG_NONE",
        _ => "REG_UNKNOWN"
    };

    private static string FormatValue(object? value, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.Binary => value is byte[] bytes
            ? BitConverter.ToString(bytes).Replace("-", " ")
            : string.Empty,
        RegistryValueKind.MultiString => value is string[] strs
            ? string.Join(" | ", strs)
            : string.Empty,
        RegistryValueKind.DWord => value is int i
            ? $"0x{i:X8} ({(uint)i})"
            : value?.ToString() ?? string.Empty,
        RegistryValueKind.QWord => value is long l
            ? $"0x{l:X16} ({(ulong)l})"
            : value?.ToString() ?? string.Empty,
        _ => value?.ToString() ?? string.Empty
    };
}
