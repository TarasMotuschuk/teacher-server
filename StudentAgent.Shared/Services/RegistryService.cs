using System.Globalization;
using System.Text;
using Microsoft.Win32;
using Teacher.Common.Contracts;

namespace StudentAgent.Services;

public sealed class RegistryService
{
    private const string DefaultValueDisplayName = "(Default)";
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
        var value = NormalizeParsedValue(ParseValue(dataStr, kind), kind);
        key.SetValue(NormalizeValueName(name), value, kind);
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

        key.DeleteValue(NormalizeValueName(name), throwOnMissingValue: false);
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

    public string ExportKey(string path)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Registry path is required.");

        using var key = OpenKey(path, writable: false);
        if (key is null)
            throw new InvalidOperationException($"Cannot open key: {path}");

        var builder = new StringBuilder();
        builder.AppendLine("Windows Registry Editor Version 5.00");
        builder.AppendLine();
        AppendExportSection(builder, path, key);
        return builder.ToString();
    }

    public ImportRegistryFileResult ImportRegFile(string regContent)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (string.IsNullOrWhiteSpace(regContent)) throw new InvalidOperationException("Registry file is empty.");

        var logicalLines = CombineMultilineEntries(regContent);
        var currentPath = string.Empty;
        var keysProcessed = 0;
        var valuesProcessed = 0;

        foreach (var rawLine in logicalLines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(';'))
            {
                continue;
            }

            if (line.StartsWith("Windows Registry Editor", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("REGEDIT4", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var section = line[1..^1];
                if (section.StartsWith('-'))
                {
                    DeleteSubKey(section[1..]);
                    currentPath = string.Empty;
                    keysProcessed++;
                    continue;
                }

                CreateKeyChain(section);
                currentPath = section;
                keysProcessed++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(currentPath))
            {
                throw new InvalidOperationException("Registry value entry appears before any key section.");
            }

            ApplyImportValue(currentPath, line);
            valuesProcessed++;
        }

        return new ImportRegistryFileResult(keysProcessed, valuesProcessed);
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
                DisplayValueName(name),
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
                DisplayValueName(name),
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

    private static RegistryKey? OpenKey(string path, bool writable)
    {
        var (hiveName, subPath) = SplitPath(path);
        var hive = OpenRootHive(hiveName);
        if (hive is null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(subPath))
        {
            return hive;
        }

        return hive.OpenSubKey(subPath, writable);
    }

    private static void CreateKeyChain(string path)
    {
        var (hiveName, subPath) = SplitPath(path);
        var hive = OpenRootHive(hiveName) ?? throw new InvalidOperationException($"Unknown hive: {hiveName}");
        if (string.IsNullOrEmpty(subPath))
        {
            return;
        }

        using var created = hive.CreateSubKey(subPath);
        if (created is null)
        {
            throw new InvalidOperationException($"Cannot create key: {path}");
        }
    }

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

    private static string DisplayValueName(string name)
        => string.IsNullOrEmpty(name) ? DefaultValueDisplayName : name;

    private static string NormalizeValueName(string? name)
        => string.Equals(name, DefaultValueDisplayName, StringComparison.Ordinal) || string.IsNullOrEmpty(name)
            ? string.Empty
            : name;

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

    private static object NormalizeParsedValue(object? value, RegistryValueKind kind) => value ?? kind switch
    {
        RegistryValueKind.Binary or RegistryValueKind.None => Array.Empty<byte>(),
        RegistryValueKind.MultiString => Array.Empty<string>(),
        RegistryValueKind.DWord => 0,
        RegistryValueKind.QWord => 0L,
        _ => string.Empty
    };

    private static void AppendExportSection(StringBuilder builder, string path, RegistryKey key)
    {
        builder.Append('[').Append(path).AppendLine("]");

        foreach (var valueName in key.GetValueNames())
        {
            var kind = key.GetValueKind(valueName);
            var value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            builder.Append(FormatRegValueName(valueName));
            builder.Append('=');
            builder.AppendLine(FormatRegValueData(value, kind));
        }

        builder.AppendLine();

        foreach (var childName in key.GetSubKeyNames())
        {
            using var child = key.OpenSubKey(childName);
            if (child is not null)
            {
                AppendExportSection(builder, $"{path}\\{childName}", child);
            }
        }
    }

    private static string FormatRegValueName(string valueName)
        => string.IsNullOrEmpty(valueName) ? "@" : $"\"{EscapeRegString(valueName)}\"";

    private static string FormatRegValueData(object? value, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.String => $"\"{EscapeRegString(value?.ToString() ?? string.Empty)}\"",
        RegistryValueKind.ExpandString => FormatHexValue("2", EncodeUnicodeWithNullTerminator(value?.ToString() ?? string.Empty)),
        RegistryValueKind.Binary => FormatHexValue(null, value as byte[] ?? []),
        RegistryValueKind.None => FormatHexValue("0", value as byte[] ?? []),
        RegistryValueKind.DWord => $"dword:{unchecked((uint)(value is int i ? i : 0)):x8}",
        RegistryValueKind.QWord => FormatHexValue("b", BitConverter.GetBytes(value is long l ? l : 0L)),
        RegistryValueKind.MultiString => FormatHexValue("7", EncodeMultiString(value as string[] ?? [])),
        _ => $"\"{EscapeRegString(value?.ToString() ?? string.Empty)}\""
    };

    private static string FormatHexValue(string? regType, byte[] bytes)
    {
        var prefix = regType is null ? "hex" : $"hex({regType})";
        return $"{prefix}:{string.Join(",", bytes.Select(static b => b.ToString("x2", CultureInfo.InvariantCulture)))}";
    }

    private static byte[] EncodeUnicodeWithNullTerminator(string value)
        => Encoding.Unicode.GetBytes($"{value}\0");

    private static byte[] EncodeMultiString(string[] values)
    {
        var joined = values.Length == 0
            ? "\0"
            : string.Join("\0", values) + "\0\0";
        return Encoding.Unicode.GetBytes(joined);
    }

    private static string EscapeRegString(string value)
        => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static IReadOnlyList<string> CombineMultilineEntries(string content)
    {
        using var reader = new StringReader(content.Replace("\r\n", "\n", StringComparison.Ordinal));
        var lines = new List<string>();
        var current = new StringBuilder();

        while (reader.ReadLine() is { } line)
        {
            if (current.Length > 0)
            {
                current.Append(line.TrimStart());
            }
            else
            {
                current.Append(line);
            }

            if (EndsWithContinuation(current))
            {
                current.Length--;
                continue;
            }

            lines.Add(current.ToString());
            current.Clear();
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return lines;
    }

    private static bool EndsWithContinuation(StringBuilder builder)
    {
        var index = builder.Length - 1;
        while (index >= 0 && char.IsWhiteSpace(builder[index]))
        {
            index--;
        }

        return index >= 0 && builder[index] == '\\';
    }

    private void ApplyImportValue(string path, string line)
    {
        var separatorIndex = line.IndexOf('=');
        if (separatorIndex < 0)
        {
            throw new InvalidOperationException($"Malformed registry value entry: {line}");
        }

        var left = line[..separatorIndex].Trim();
        var right = line[(separatorIndex + 1)..].Trim();
        var valueName = ParseRegValueName(left);

        if (right == "-")
        {
            DeleteValue(path, valueName);
            return;
        }

        var (kind, value) = ParseRegValueData(right);
        using var key = OpenKey(path, writable: true) ?? throw new InvalidOperationException($"Cannot open key: {path}");
        key.SetValue(valueName, value, kind);
    }

    private static string ParseRegValueName(string token)
    {
        if (token == "@")
        {
            return string.Empty;
        }

        if (token.Length >= 2 && token[0] == '"' && token[^1] == '"')
        {
            return UnescapeRegString(token[1..^1]);
        }

        throw new InvalidOperationException($"Unsupported registry value name token: {token}");
    }

    private static (RegistryValueKind kind, object value) ParseRegValueData(string token)
    {
        if (token.Length >= 2 && token[0] == '"' && token[^1] == '"')
        {
            return (RegistryValueKind.String, UnescapeRegString(token[1..^1]));
        }

        if (token.StartsWith("dword:", StringComparison.OrdinalIgnoreCase))
        {
            var hex = token["dword:".Length..];
            var parsed = uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return (RegistryValueKind.DWord, unchecked((int)parsed));
        }

        if (token.StartsWith("hex", StringComparison.OrdinalIgnoreCase))
        {
            var colonIndex = token.IndexOf(':');
            if (colonIndex < 0)
            {
                throw new InvalidOperationException($"Malformed hex registry value: {token}");
            }

            var typeToken = token[3..colonIndex].Trim();
            var bytes = ParseHexByteList(token[(colonIndex + 1)..]);
            return typeToken.ToLowerInvariant() switch
            {
                "" => (RegistryValueKind.Binary, bytes),
                "(0)" => (RegistryValueKind.None, bytes),
                "(2)" => (RegistryValueKind.ExpandString, DecodeUnicodeString(bytes)),
                "(7)" => (RegistryValueKind.MultiString, DecodeMultiString(bytes)),
                "(b)" => (RegistryValueKind.QWord, bytes.Length >= 8 ? BitConverter.ToInt64(bytes, 0) : 0L),
                _ => (RegistryValueKind.Binary, bytes)
            };
        }

        throw new InvalidOperationException($"Unsupported registry value data: {token}");
    }

    private static byte[] ParseHexByteList(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return [];
        }

        return [.. token
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(value => byte.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture))];
    }

    private static string DecodeUnicodeString(byte[] bytes)
    {
        var decoded = Encoding.Unicode.GetString(bytes);
        return decoded.TrimEnd('\0');
    }

    private static string[] DecodeMultiString(byte[] bytes)
    {
        var decoded = Encoding.Unicode.GetString(bytes).TrimEnd('\0');
        return string.IsNullOrEmpty(decoded)
            ? []
            : decoded.Split('\0', StringSplitOptions.None);
    }

    private static string UnescapeRegString(string value)
    {
        var builder = new StringBuilder(value.Length);
        var escape = false;

        foreach (var ch in value)
        {
            if (escape)
            {
                builder.Append(ch);
                escape = false;
            }
            else if (ch == '\\')
            {
                escape = true;
            }
            else
            {
                builder.Append(ch);
            }
        }

        if (escape)
        {
            builder.Append('\\');
        }

        return builder.ToString();
    }

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
