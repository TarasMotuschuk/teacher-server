using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Teacher.Common.Localization;

namespace TeacherClient.CrossPlatform.Services;

internal static class TestClassroomLaunchHelper
{
    public const string DefaultRemoteDirectory = @"C:\Users\Public\ClassCommander\TestRunner";

    public const string RemoteExeName = "ClassCommander.TestRunner.exe";

    public static string RemoteExePath => Path.Combine(DefaultRemoteDirectory, RemoteExeName);

    public static string ResolveClassroomServerUrl(string configuredBaseUrl)
    {
        if (!Uri.TryCreate(configuredBaseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid Test Platform URL: {configuredBaseUrl}");
        }

        var host = uri.Host;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase))
        {
            var lanIp = GetPreferredLanIpv4()
                ?? throw new InvalidOperationException("Could not determine a LAN IPv4 address for Test Platform.");
            host = lanIp;
        }

        var builder = new UriBuilder(uri)
        {
            Host = host,
            Path = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty,
        };
        return builder.Uri.ToString().TrimEnd('/');
    }

    public static string BuildLaunchScript(
        string classroomServerUrl,
        string className,
        DiscoveredAgentRow agent)
    {
        var deviceId = string.IsNullOrWhiteSpace(agent.MachineName) ? agent.AgentId : agent.MachineName;
        var surname = deviceId;
        var name = ExtractUserName(agent.CurrentUser);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Student";
        }

        var args = string.Join(
            " ",
            QuoteArg("--server-url"),
            QuoteArg(classroomServerUrl),
            QuoteArg("--class"),
            QuoteArg(className),
            QuoteArg("--device-id"),
            QuoteArg(deviceId),
            QuoteArg("--surname"),
            QuoteArg(surname),
            QuoteArg("--name"),
            QuoteArg(name),
            QuoteArg("--language"),
            QuoteArg(ClassCommanderUiSettings.LoadLanguage().ToCode()),
            QuoteArg("--auto-continue"));

        return $"start \"\" {QuoteArg(RemoteExePath)} {args}";
    }

    public static string? FindLocalRunnerDirectory()
    {
        foreach (var root in EnumerateRepositoryRoots())
        {
            foreach (var config in new[] { "Release", "Debug" })
            {
                var candidates = new[]
                {
                    Path.Combine(root, "ClassCommander.TestRunner", "bin", config, "net10.0", "win-x64", "publish"),
                    Path.Combine(root, "ClassCommander.TestRunner", "bin", config, "net10.0", "win-x64"),
                    Path.Combine(root, "ClassCommander.TestRunner", "bin", config, "net10.0"),
                };

                foreach (var dir in candidates)
                {
                    if (!Directory.Exists(dir))
                    {
                        continue;
                    }

                    var exeWin = Path.Combine(dir, RemoteExeName);
                    var dll = Path.Combine(dir, "ClassCommander.TestRunner.dll");
                    if (!File.Exists(exeWin) && !File.Exists(dll))
                    {
                        continue;
                    }

                    // Classroom students are Windows; avoid deploying a macOS/Linux build by mistake.
                    if (!OperatingSystem.IsWindows() && !File.Exists(exeWin))
                    {
                        continue;
                    }

                    return dir;
                }
            }
        }

        return null;
    }

    private static string ExtractUserName(string? currentUser)
    {
        if (string.IsNullOrWhiteSpace(currentUser))
        {
            return string.Empty;
        }

        var value = currentUser.Trim();
        var slash = value.LastIndexOf('\\');
        if (slash >= 0 && slash < value.Length - 1)
        {
            return value[(slash + 1)..];
        }

        var at = value.IndexOf('@');
        if (at > 0)
        {
            return value[..at];
        }

        return value;
    }

    private static string QuoteArg(string value) => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private static string? GetPreferredLanIpv4()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni =>
                ni.OperationalStatus == OperationalStatus.Up
                && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
            .Where(ua =>
                ua.Address.AddressFamily == AddressFamily.InterNetwork
                && !IPAddress.IsLoopback(ua.Address)
                && !ua.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
            .Select(ua => ua.Address.ToString())
            .FirstOrDefault();
    }

    private static IEnumerable<string> EnumerateRepositoryRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (!seen.Add(dir.FullName))
            {
                continue;
            }

            if (File.Exists(Path.Combine(dir.FullName, "TeacherServer.sln")))
            {
                yield return dir.FullName;
            }
        }
    }
}
