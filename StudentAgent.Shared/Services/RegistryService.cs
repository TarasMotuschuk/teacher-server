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

    public IReadOnlyList<RegistryValueEditDto> GetValuesForEdit(string path)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();

        if (string.IsNullOrWhiteSpace(path)) return [];

        var (hiveName, subPath) = SplitPath(path);
        var hive = OpenRootHive(hiveName);
        if (hive is null) return [];

        if (string.IsNullOrEmpty(subPath))
            return ListValuesForEdit(hive);

        using var key = hive.OpenSubKey(subPath);
        return key is null ? [] : ListValuesForEdit(key);
    }

    public void SetValue(string path, string name, string typeStr, string dataStr)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();

        var (hiveName, subPath) = SplitPath(path);
        var hive = OpenRootHive(hiveName);
        if (hive is null) throw new InvalidOperationException($"Unknown hive: {hiveName}");

        using var key = string.IsNullOrEmpty(subPath)
            ? hive
            : hive.OpenSubKey(subPath, writable: true);

        if (key is null)
            throw new InvalidOperationException($"Cannot open key: {path}");

        var kind = ParseKind(typeStr);
        var value = ParseValue(dataStr, kind);
        key.SetValue(string.IsNullOrEmpty(name) ? "" : name, value, kind);
    }

    public void DeleteValue(string path, string name)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();

        var (hiveName, subPath) = SplitPath(path);
        var hive = OpenRootHive(hiveName);
        if (hive is null) throw new InvalidOperationException($"Unknown hive: {hiveName}");

        using var key = string.IsNullOrEmpty(subPath)
            ? hive
            : hive.OpenSubKey(subPath, writable: true);

        if (key is null)
            throw new InvalidOperationException($"Cannot open key: {path}");

        key.DeleteValue(string.IsNullOrEmpty(name) ? "" : name, throwOnMissingValue: false);
    }

    public void CreateSubKey(string parentPath, string keyName)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();

        var (hiveName, subPath) = SplitPath(parentPath);
        var hive = OpenRootHive(hiveName);
        if (hive is null) throw new InvalidOperationException($"Unknown hive: {hiveName}");

        using var parentKey = string.IsNullOrEmpty(subPath)
            ? hive
            : hive.OpenSubKey(subPath, writable: true);

        if (parentKey is null)
            throw new InvalidOperationException($"Cannot open key: {parentPath}");

        parentKey.CreateSubKey(keyName);
    }

    public void DeleteSubKey(string path)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();

        var (hiveName, subPath) = SplitPath(path);
        var hive = OpenRootHive(hiveName);
        if (hive is null) throw new InvalidOperationException($"Unknown hive: {hiveName}");

        if (string.IsNullOrEmpty(subPath))
            throw new InvalidOperationException("Cannot delete root hive");

        var lastSlash = subPath.LastIndexOf('\\');
        var parentPath = lastSlash < 0 ? "" : subPath[..lastSlash];
        var keyName = lastSlash < 0 ? subPath : subPath[(lastSlash + 1)..];

        using var parentKey = string.IsNullOrEmpty(parentPath)
            ? hive
            : hive.OpenSubKey(parentPath, writable: true);

        if (parentKey is null)
            throw new InvalidOperationException($"Cannot open parent key: {parentPath}");

        parentKey.DeleteSubKey(keyName, throwOnMissingSubKey: false);
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

    private static IReadOnlyList<RegistryValueEditDto> ListValuesForEdit(RegistryKey key)
    {
        return [.. key.GetValueNames().Select(name =>
        {
            var kind = key.GetValueKind(name);
            var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            return new RegistryValueEditDto(
                string.IsNullOrEmpty(name) ? "(Default)" : name,
                FormatKind(kind),
                FormatValue(value, kind),
                FormatKind(kind),
                EncodeValue(value, kind));
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

    private static string EncodeValue(object? value, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.Binary => value is byte[] bytes
            ? BitConverter.ToString(bytes).Replace("-", "")
            : string.Empty,
        RegistryValueKind.MultiString => value is string[] strs
            ? string.Join("\0", strs)
            : string.Empty,
        RegistryValueKind.DWord => value is int i
            ? i.ToString()
            : value?.ToString() ?? string.Empty,
        RegistryValueKind.QWord => value is long l
            ? l.ToString()
            : value?.ToString() ?? string.Empty,
        _ => value?.ToString() ?? string.Empty
    };

    private static RegistryValueKind ParseKind(string typeStr) => typeStr.ToUpperInvariant() switch
    {
        "REG_SZ" => RegistryValueKind.String,
        "REG_EXPAND_SZ" => RegistryValueKind.ExpandString,
        "REG_BINARY" => RegistryValueKind.Binary,
        "REG_DWORD" => RegistryValueKind.DWord,
        "REG_MULTI_SZ" => RegistryValueKind.MultiString,
        "REG_QWORD" => RegistryValueKind.QWord,
        "REG_NONE" => RegistryValueKind.None,
        _ => RegistryValueKind.String
    };

    private static object? ParseValue(string dataStr, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.Binary => HexStringToBytes(dataStr),
        RegistryValueKind.MultiString => string.IsNullOrEmpty(dataStr)
            ? []
            : dataStr.Split('\0', StringSplitOptions.RemoveEmptyEntries),
        RegistryValueKind.DWord => int.TryParse(dataStr, System.Globalization.NumberStyles.Any, null, out var i)
            ? i
            : 0,
        RegistryValueKind.QWord => long.TryParse(dataStr, System.Globalization.NumberStyles.Any, null, out var l)
            ? l
            : 0L,
        _ => dataStr ?? string.Empty
    };

    private static byte[] HexStringToBytes(string hex)
    {
        hex = hex.Replace(" ", "").Replace("-", "");
        if (hex.Length % 2 != 0) hex = "0" + hex;
        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }
}
