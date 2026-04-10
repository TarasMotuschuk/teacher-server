using System.Reflection;
using System.Runtime.InteropServices;
using Teacher.Common.Contracts;

namespace StudentAgent.Services;

public sealed class ServerInfoService
{
    private const int WtsUserName = 5;
    private const int WtsDomainName = 7;
    private static readonly string _agentVersion =
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";

    private readonly AgentSettingsStore _settingsStore;

    public ServerInfoService(AgentSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public ServerInfoDto GetInfo()
    {
        var machineName = Environment.MachineName;
        var currentUser = GetPreferredCurrentUser(machineName);
        var osDescription = RuntimeInformation.OSDescription;

        return new ServerInfoDto(
            machineName,
            currentUser,
            osDescription,
            DateTime.UtcNow,
            true,
            _settingsStore.Current.BrowserLockEnabled,
            _settingsStore.Current.InputLockEnabled,
            _agentVersion);
    }

    private static string GetPreferredCurrentUser(string machineName)
    {
        var interactiveUser = TryGetActiveSessionUser();
        if (!string.IsNullOrWhiteSpace(interactiveUser))
        {
            return interactiveUser;
        }

        return NormalizeUserDisplay(Environment.UserName, machineName);
    }

    private static string? TryGetActiveSessionUser()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var sessionId = WTSGetActiveConsoleSessionId();
            if (sessionId == 0xFFFFFFFF)
            {
                return null;
            }

            var userName = QuerySessionString((int)sessionId, WtsUserName);
            if (string.IsNullOrWhiteSpace(userName))
            {
                return null;
            }

            var domainName = QuerySessionString((int)sessionId, WtsDomainName);
            return string.IsNullOrWhiteSpace(domainName)
                ? userName
                : $"{domainName}\\{userName}";
        }
        catch
        {
            return null;
        }
    }

    private static string? QuerySessionString(int sessionId, int infoClass)
    {
        if (!WTSQuerySessionInformationW(IntPtr.Zero, sessionId, infoClass, out var buffer, out var bytesReturned) ||
            buffer == IntPtr.Zero ||
            bytesReturned <= 1)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUni(buffer)?.Trim();
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    private static string NormalizeUserDisplay(string? currentUser, string machineName)
    {
        if (string.IsNullOrWhiteSpace(currentUser))
        {
            return string.Empty;
        }

        var trimmed = currentUser.Trim();
        var accountName = trimmed.Contains('\\', StringComparison.Ordinal)
            ? trimmed[(trimmed.LastIndexOf('\\') + 1)..]
            : trimmed;

        if (string.Equals(accountName, $"{machineName}$", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return trimmed;
    }

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool WTSQuerySessionInformationW(
        IntPtr hServer,
        int sessionId,
        int wtsInfoClass,
        out IntPtr ppBuffer,
        out int pBytesReturned);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr pointer);
}
