using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using StudentAgent.Services;
using Teacher.Common.Contracts;

namespace StudentAgent.Service.Services;

public sealed class BrowserCleanupService
{
    private readonly AgentLogService _logService;

    public BrowserCleanupService(AgentLogService logService)
    {
        _logService = logService;
    }

    public BrowserCleanupResultDto ClearHistoryAndCache(BrowserCleanupRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Browser cleanup is only supported on Windows student agents.");
        }

        var errors = new List<string>();
        var cleared = new List<string>();
        var stopped = new List<string>();

        var sessionId = SessionProcessLauncher.GetActiveSessionId();
        if (sessionId < 0)
        {
            return new BrowserCleanupResultDto(false, "No active interactive user session.", [], [], ["No active session."]);
        }

        var profilePath = TryGetUserProfileDirectoryForSession(sessionId, out var profile, out var profileErr)
            ? profile
            : null;
        if (profilePath is null)
        {
            return new BrowserCleanupResultDto(false, "Could not resolve user profile directory.", [], [], [profileErr ?? "Unknown profile error."]);
        }

        // Close browsers first so SQLite DBs and cache directories are not locked.
        foreach (var image in new[] { "chrome", "msedge", "firefox", "opera", "opera_gx" })
        {
            if (StopProcessesInSessionByName(image, sessionId))
            {
                stopped.Add(image);
            }
        }

        var local = Path.Combine(profilePath, "AppData", "Local");
        var roaming = Path.Combine(profilePath, "AppData", "Roaming");

        if (request.ClearHistory || request.ClearCache)
        {
            // Chromium family: Chrome, Edge, Opera.
            ClearChromiumFamily(local, roaming, request, cleared, errors);

            // Firefox: clear cache via cache2; clear history via places.sqlite (preserving bookmarks).
            ClearFirefox(roaming, request, cleared, errors);
        }

        var ok = errors.Count == 0;
        var msg = ok ? "Browser cleanup completed." : "Browser cleanup completed with errors.";
        _logService.LogInfo($"Browser cleanup: {msg} cleared={cleared.Count}, errors={errors.Count}.");
        if (errors.Count > 0)
        {
            foreach (var e in errors.Take(10))
            {
                _logService.LogWarning($"Browser cleanup warning: {e}");
            }
        }

        return new BrowserCleanupResultDto(ok, msg, stopped, cleared, errors);
    }

    public BrowserCookiesCleanupResultDto ClearCookies(BrowserCookiesCleanupRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Browser cookies cleanup is only supported on Windows student agents.");
        }

        var errors = new List<string>();
        var cleared = new List<string>();
        var stopped = new List<string>();

        var sessionId = SessionProcessLauncher.GetActiveSessionId();
        if (sessionId < 0)
        {
            return new BrowserCookiesCleanupResultDto(false, "No active interactive user session.", [], [], ["No active session."]);
        }

        var profilePath = TryGetUserProfileDirectoryForSession(sessionId, out var profile, out var profileErr)
            ? profile
            : null;
        if (profilePath is null)
        {
            return new BrowserCookiesCleanupResultDto(false, "Could not resolve user profile directory.", [], [], [profileErr ?? "Unknown profile error."]);
        }

        foreach (var image in new[] { "chrome", "msedge", "firefox", "opera", "opera_gx" })
        {
            if (StopProcessesInSessionByName(image, sessionId))
            {
                stopped.Add(image);
            }
        }

        var local = Path.Combine(profilePath, "AppData", "Local");
        var roaming = Path.Combine(profilePath, "AppData", "Roaming");

        ClearChromiumCookies(local, roaming, cleared, errors);
        ClearFirefoxCookies(roaming, cleared, errors);

        var ok = errors.Count == 0;
        var msg = ok ? "Browser cookies cleanup completed." : "Browser cookies cleanup completed with errors.";
        _logService.LogInfo($"Browser cookies cleanup: {msg} cleared={cleared.Count}, errors={errors.Count}.");
        if (errors.Count > 0)
        {
            foreach (var e in errors.Take(10))
            {
                _logService.LogWarning($"Browser cookies cleanup warning: {e}");
            }
        }

        return new BrowserCookiesCleanupResultDto(ok, msg, stopped, cleared, errors);
    }

    private static void ClearChromiumFamily(
        string localAppData,
        string roamingAppData,
        BrowserCleanupRequest request,
        List<string> cleared,
        List<string> errors)
    {
        // Chrome.
        ClearChromiumUserDataRoot(
            Path.Combine(localAppData, "Google", "Chrome", "User Data"),
            "Chrome",
            request,
            cleared,
            errors);

        // Edge.
        ClearChromiumUserDataRoot(
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data"),
            "Edge",
            request,
            cleared,
            errors);

        // Opera stable (Roaming).
        ClearChromiumProfileDir(
            Path.Combine(roamingAppData, "Opera Software", "Opera Stable"),
            "Opera Stable",
            request,
            cleared,
            errors);

        // Opera GX (Roaming).
        ClearChromiumProfileDir(
            Path.Combine(roamingAppData, "Opera Software", "Opera GX Stable"),
            "Opera GX Stable",
            request,
            cleared,
            errors);
    }

    private static void ClearChromiumCookies(string localAppData, string roamingAppData, List<string> cleared, List<string> errors)
    {
        ClearChromiumCookiesUserDataRoot(Path.Combine(localAppData, "Google", "Chrome", "User Data"), "Chrome", cleared, errors);
        ClearChromiumCookiesUserDataRoot(Path.Combine(localAppData, "Microsoft", "Edge", "User Data"), "Edge", cleared, errors);

        ClearChromiumCookiesProfileDir(
            Path.Combine(roamingAppData, "Opera Software", "Opera Stable"),
            "Opera Stable",
            cleared,
            errors);
        ClearChromiumCookiesProfileDir(
            Path.Combine(roamingAppData, "Opera Software", "Opera GX Stable"),
            "Opera GX Stable",
            cleared,
            errors);
    }

    private static void ClearChromiumCookiesUserDataRoot(string userDataRoot, string label, List<string> cleared, List<string> errors)
    {
        if (!Directory.Exists(userDataRoot))
        {
            return;
        }

        ClearChromiumCookiesProfileDir(Path.Combine(userDataRoot, "Default"), $"{label} Default", cleared, errors);
        foreach (var profileDir in Directory.GetDirectories(userDataRoot, "Profile *"))
        {
            ClearChromiumCookiesProfileDir(profileDir, $"{label} {Path.GetFileName(profileDir)}", cleared, errors);
        }
    }

    private static void ClearChromiumCookiesProfileDir(string profileDir, string label, List<string> cleared, List<string> errors)
    {
        if (!Directory.Exists(profileDir))
        {
            return;
        }

        // Cookies DB moved under Network for newer Chromium; keep both best-effort.
        TryDeleteFile(Path.Combine(profileDir, "Cookies"), $"{label}: Cookies", cleared, errors);
        TryDeleteFile(Path.Combine(profileDir, "Cookies-journal"), $"{label}: Cookies-journal", cleared, errors);
        TryDeleteFile(Path.Combine(profileDir, "Network", "Cookies"), $"{label}: Network Cookies", cleared, errors);
        TryDeleteFile(Path.Combine(profileDir, "Network", "Cookies-journal"), $"{label}: Network Cookies-journal", cleared, errors);
    }

    private static void ClearFirefoxCookies(string roamingAppData, List<string> cleared, List<string> errors)
    {
        var profilesRoot = Path.Combine(roamingAppData, "Mozilla", "Firefox", "Profiles");
        if (!Directory.Exists(profilesRoot))
        {
            return;
        }

        foreach (var profileDir in Directory.GetDirectories(profilesRoot))
        {
            TryDeleteFile(Path.Combine(profileDir, "cookies.sqlite"), $"Firefox {Path.GetFileName(profileDir)}: cookies.sqlite", cleared, errors);
            TryDeleteFile(Path.Combine(profileDir, "cookies.sqlite-wal"), $"Firefox {Path.GetFileName(profileDir)}: cookies.sqlite-wal", cleared, errors);
            TryDeleteFile(Path.Combine(profileDir, "cookies.sqlite-shm"), $"Firefox {Path.GetFileName(profileDir)}: cookies.sqlite-shm", cleared, errors);
        }
    }

    private static void ClearChromiumUserDataRoot(
        string userDataRoot,
        string label,
        BrowserCleanupRequest request,
        List<string> cleared,
        List<string> errors)
    {
        if (!Directory.Exists(userDataRoot))
        {
            return;
        }

        ClearChromiumProfileDir(Path.Combine(userDataRoot, "Default"), $"{label} Default", request, cleared, errors);
        foreach (var profileDir in Directory.GetDirectories(userDataRoot, "Profile *"))
        {
            ClearChromiumProfileDir(profileDir, $"{label} {Path.GetFileName(profileDir)}", request, cleared, errors);
        }
    }

    private static void ClearChromiumProfileDir(
        string profileDir,
        string label,
        BrowserCleanupRequest request,
        List<string> cleared,
        List<string> errors)
    {
        if (!Directory.Exists(profileDir))
        {
            return;
        }

        if (request.ClearHistory)
        {
            TryDeleteFile(Path.Combine(profileDir, "History"), $"{label}: History", cleared, errors);
            TryDeleteFile(Path.Combine(profileDir, "History-journal"), $"{label}: History-journal", cleared, errors);
            TryDeleteFile(Path.Combine(profileDir, "Visited Links"), $"{label}: Visited Links", cleared, errors);
            TryDeleteFile(Path.Combine(profileDir, "Top Sites"), $"{label}: Top Sites", cleared, errors);
        }

        if (request.ClearCache)
        {
            TryDeleteDirectory(Path.Combine(profileDir, "Cache"), $"{label}: Cache", cleared, errors);
            TryDeleteDirectory(Path.Combine(profileDir, "Code Cache"), $"{label}: Code Cache", cleared, errors);
            TryDeleteDirectory(Path.Combine(profileDir, "GPUCache"), $"{label}: GPUCache", cleared, errors);
            TryDeleteDirectory(Path.Combine(profileDir, "ShaderCache"), $"{label}: ShaderCache", cleared, errors);
            TryDeleteDirectory(Path.Combine(profileDir, "Service Worker", "CacheStorage"), $"{label}: ServiceWorker CacheStorage", cleared, errors);
            TryDeleteDirectory(Path.Combine(profileDir, "Storage", "ext"), $"{label}: Storage ext", cleared, errors);
            TryDeleteDirectory(Path.Combine(profileDir, "Network", "Cache"), $"{label}: Network Cache", cleared, errors);
        }
    }

    private static void ClearFirefox(
        string roamingAppData,
        BrowserCleanupRequest request,
        List<string> cleared,
        List<string> errors)
    {
        var profilesRoot = Path.Combine(roamingAppData, "Mozilla", "Firefox", "Profiles");
        if (!Directory.Exists(profilesRoot))
        {
            return;
        }

        foreach (var profileDir in Directory.GetDirectories(profilesRoot))
        {
            if (request.ClearCache)
            {
                TryDeleteDirectory(Path.Combine(profileDir, "cache2"), $"Firefox {Path.GetFileName(profileDir)}: cache2", cleared, errors);
            }

            if (request.ClearHistory)
            {
                var places = Path.Combine(profileDir, "places.sqlite");
                if (File.Exists(places))
                {
                    try
                    {
                        ClearFirefoxPlacesHistory(places);
                        cleared.Add($"Firefox {Path.GetFileName(profileDir)}: history");
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Firefox {Path.GetFileName(profileDir)}: failed to clear history ({ex.Message})");
                    }
                }
            }
        }
    }

    private static void ClearFirefoxPlacesHistory(string placesSqlitePath)
    {
        // Preserve bookmarks: moz_places.foreign_count > 0 indicates referenced by bookmarks.
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = placesSqlitePath,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
        }.ToString();

        using var conn = new SqliteConnection(cs);
        conn.Open();

        using var tx = conn.BeginTransaction();
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
DELETE FROM moz_historyvisits;
DELETE FROM moz_inputhistory;
DELETE FROM moz_places
WHERE foreign_count = 0
  AND last_visit_date IS NOT NULL;
""";
            cmd.ExecuteNonQuery();
        }

        tx.Commit();

        using (var vacuum = conn.CreateCommand())
        {
            vacuum.CommandText = "VACUUM;";
            vacuum.ExecuteNonQuery();
        }
    }

    private static void TryDeleteFile(string path, string label, List<string> cleared, List<string> errors)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                cleared.Add(label);
            }
        }
        catch (Exception ex)
        {
            errors.Add($"{label} delete failed: {ex.Message}");
        }
    }

    private static void TryDeleteDirectory(string path, string label, List<string> cleared, List<string> errors)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
                cleared.Add(label);
            }
        }
        catch (Exception ex)
        {
            errors.Add($"{label} delete failed: {ex.Message}");
        }
    }

    private static bool StopProcessesInSessionByName(string processNameWithoutExtension, int sessionId)
    {
        var stoppedAny = false;
        foreach (var process in Process.GetProcessesByName(processNameWithoutExtension))
        {
            try
            {
                if (process.SessionId != sessionId)
                {
                    continue;
                }

                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
                stoppedAny = true;
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return stoppedAny;
    }

    private static bool TryGetUserProfileDirectoryForSession(int sessionId, out string? profileDir, out string? error)
    {
        profileDir = null;
        error = null;

        if (!WTSQueryUserToken(sessionId, out var token) || token == IntPtr.Zero)
        {
            error = "WTSQueryUserToken failed.";
            return false;
        }

        try
        {
            var capacity = 260;
            var sb = new System.Text.StringBuilder(capacity);
            if (!GetUserProfileDirectory(token, sb, ref capacity))
            {
                var err = Marshal.GetLastWin32Error();
                error = new Win32Exception(err).Message;
                return false;
            }

            var path = sb.ToString();
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                error = "User profile path is empty or missing.";
                return false;
            }

            profileDir = path;
            return true;
        }
        finally
        {
            CloseHandle(token);
        }
    }

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(int SessionId, out IntPtr phToken);

    [DllImport("userenv.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetUserProfileDirectory(IntPtr hToken, System.Text.StringBuilder lpProfileDir, ref int lpcchSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}

