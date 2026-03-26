namespace Teacher.Common;

public static class RemoteWindowsPath
{
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Trim().Replace('/', '\\');
        if (IsDriveRoot(normalized))
        {
            return normalized.EndsWith('\\') ? normalized : $"{normalized}\\";
        }

        return normalized.TrimEnd('\\');
    }

    public static string Combine(string left, string right)
    {
        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right).TrimStart('\\');

        if (string.IsNullOrWhiteSpace(normalizedLeft))
        {
            return normalizedRight;
        }

        if (string.IsNullOrWhiteSpace(normalizedRight))
        {
            return normalizedLeft;
        }

        return IsDriveRoot(normalizedLeft)
            ? $"{normalizedLeft}{normalizedRight}"
            : $"{normalizedLeft}\\{normalizedRight}";
    }

    public static string CombineSegments(string root, string relativePath)
    {
        var result = Normalize(root);
        foreach (var segment in relativePath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries))
        {
            result = Combine(result, segment);
        }

        return result;
    }

    public static bool TryGetParentAndName(string path, out string parent, out string name)
    {
        var normalized = Normalize(path);
        if (string.IsNullOrWhiteSpace(normalized) || IsDriveRoot(normalized))
        {
            parent = string.Empty;
            name = string.Empty;
            return false;
        }

        var separatorIndex = normalized.LastIndexOf('\\');
        if (separatorIndex < 0)
        {
            parent = string.Empty;
            name = normalized;
            return !string.IsNullOrWhiteSpace(name);
        }

        if (separatorIndex == 2 && normalized.Length >= 3 && normalized[1] == ':')
        {
            parent = normalized[..3];
        }
        else
        {
            parent = normalized[..separatorIndex];
        }

        name = normalized[(separatorIndex + 1)..];
        return !string.IsNullOrWhiteSpace(parent) && !string.IsNullOrWhiteSpace(name);
    }

    public static bool IsDriveRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = path.Trim().Replace('/', '\\');
        return normalized.Length is 2 or 3 &&
               char.IsLetter(normalized[0]) &&
               normalized[1] == ':' &&
               (normalized.Length == 2 || normalized[2] == '\\');
    }
}
