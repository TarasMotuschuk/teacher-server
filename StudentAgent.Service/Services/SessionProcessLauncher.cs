using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StudentAgent.Service.Services;

/// <summary>
/// Launches processes in interactive sessions from the Windows service (session 0).
/// Mirrors the Veyon model: the service stays in session 0; session work runs in child processes.
/// For remote desktop / login-screen capable starts, Veyon uses <c>winlogon.exe</c> in the target
/// session as the token source (<see cref="StartProcessInSessionUsingWinlogonToken"/>); plain
/// <c>WTSQueryUserToken</c> only works after a user has logged on.
/// </summary>
internal static class SessionProcessLauncher
{
    private const int NormalPriorityClass = 0x00000020;
    private const int CreateUnicodeEnvironment = 0x00000400;
    private const int CreateNoWindow = 0x08000000;

    internal enum SessionProcessLaunchMode
    {
        WinlogonToken,
        UserTokenFallback,
    }

    public static int GetActiveSessionId()
    {
        var sessionId = WTSGetActiveConsoleSessionId();
        return sessionId == 0xFFFFFFFF ? -1 : unchecked((int)sessionId);
    }

    /// <summary>
    /// Starts a process in the session using the same approach as Veyon: duplicate the access token
    /// from <c>winlogon.exe</c> in that session so the child can run on the logon desktop before any
    /// user session token exists. Falls back to <c>WTSQueryUserToken</c> if winlogon-based launch fails.
    /// </summary>
    /// <returns></returns>
    public static SessionProcessLaunchMode StartProcessInSessionPreferWinlogon(string applicationPath, string arguments, int sessionId, bool hideWindow = false)
    {
        if (TryStartProcessUsingWinlogonToken(applicationPath, arguments, sessionId, hideWindow, waitForExit: false, timeout: null, out _))
        {
            return SessionProcessLaunchMode.WinlogonToken;
        }

        StartProcessInSession(applicationPath, arguments, sessionId, hideWindow);
        return SessionProcessLaunchMode.UserTokenFallback;
    }

    public static void StartProcessInSession(string applicationPath, string arguments, int sessionId)
        => StartProcessInSession(applicationPath, arguments, sessionId, hideWindow: false);

    public static void StartProcessInSession(string applicationPath, string arguments, int sessionId, bool hideWindow)
        => _ = StartProcessInSessionInternal(applicationPath, arguments, sessionId, hideWindow, waitForExit: false, timeout: null);

    public static int StartProcessInSessionAndWait(string applicationPath, string arguments, int sessionId, bool hideWindow, TimeSpan timeout)
        => StartProcessInSessionInternal(applicationPath, arguments, sessionId, hideWindow, waitForExit: true, timeout);

    private static bool TryFindWinlogonProcessId(int sessionId, out int processId)
    {
        processId = 0;
        foreach (var process in Process.GetProcessesByName("winlogon"))
        {
            try
            {
                if (process.SessionId == sessionId)
                {
                    processId = process.Id;
                    return true;
                }
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return false;
    }

    /// <summary>Veyon-style launch: token from winlogon in the session (works at the Windows logon screen).</summary>
    private static bool TryStartProcessUsingWinlogonToken(
        string applicationPath,
        string arguments,
        int sessionId,
        bool hideWindow,
        bool waitForExit,
        TimeSpan? timeout,
        out int exitCode)
    {
        exitCode = 0;
        if (!TryFindWinlogonProcessId(sessionId, out var winlogonPid))
        {
            return false;
        }

        EnableLaunchPrivilegesBestEffort();

        IntPtr winlogonProcess = OpenProcess(0x1F0FFF, false, winlogonPid);
        if (winlogonProcess == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (!OpenProcessToken(winlogonProcess, 0x02000000, out var userProcessToken))
            {
                return false;
            }

            try
            {
                if (!CreateEnvironmentBlock(out var userEnvironment, userProcessToken, false))
                {
                    return false;
                }

                try
                {
                    var startupInfo = new STARTUPINFO
                    {
                        Cb = Marshal.SizeOf<STARTUPINFO>(),
                        LpDesktop = @"winsta0\default",
                        DwFlags = hideWindow ? 0x00000001 : 0,
                        WShowWindow = hideWindow ? (short)0 : (short)1,
                    };

                    var commandLine = string.IsNullOrWhiteSpace(arguments)
                        ? $"\"{applicationPath}\""
                        : $"\"{applicationPath}\" {arguments}";

                    var creationFlags = CreateUnicodeEnvironment | NormalPriorityClass;
                    if (hideWindow)
                    {
                        creationFlags |= CreateNoWindow;
                    }

                    if (!DuplicateTokenEx(
                            userProcessToken,
                            0x02000000 | 0x000F0000 | 0x00000008 | 0x00000002 | 0x00000001,
                            IntPtr.Zero,
                            2,
                            1,
                            out var newToken))
                    {
                        return false;
                    }

                    try
                    {
                        if (!CreateProcessAsUser(
                                newToken,
                                null,
                                commandLine,
                                IntPtr.Zero,
                                IntPtr.Zero,
                                false,
                                creationFlags,
                                userEnvironment,
                                Path.GetDirectoryName(applicationPath),
                                ref startupInfo,
                                out var processInformation))
                        {
                            return false;
                        }

                        try
                        {
                            if (!waitForExit)
                            {
                                return true;
                            }

                            var waitMilliseconds = timeout is null
                                ? Timeout.Infinite
                                : checked((int)Math.Clamp(timeout.Value.TotalMilliseconds, 1, int.MaxValue));

                            var waitResult = WaitForSingleObject(processInformation.HProcess, waitMilliseconds);
                            if (waitResult == WAITTIMEOUT)
                            {
                                throw new TimeoutException($"Process '{applicationPath}' did not finish within {timeout}.");
                            }

                            if (!GetExitCodeProcess(processInformation.HProcess, out var code))
                            {
                                throw new Win32Exception(Marshal.GetLastWin32Error(), "GetExitCodeProcess failed.");
                            }

                            exitCode = unchecked((int)code);
                            return true;
                        }
                        finally
                        {
                            CloseHandle(processInformation.HThread);
                            CloseHandle(processInformation.HProcess);
                        }
                    }
                    finally
                    {
                        CloseHandle(newToken);
                    }
                }
                finally
                {
                    DestroyEnvironmentBlock(userEnvironment);
                }
            }
            finally
            {
                CloseHandle(userProcessToken);
            }
        }
        finally
        {
            CloseHandle(winlogonProcess);
        }
    }

    private static void EnableLaunchPrivilegesBestEffort()
    {
        foreach (var name in new[] { "SeAssignPrimaryTokenPrivilege", "SeIncreaseQuotaPrivilege", "SeTcbPrivilege" })
        {
            TryEnablePrivilege(name);
        }
    }

    private static void TryEnablePrivilege(string privilegeName)
    {
        if (!OpenProcessToken(GetCurrentProcess(), 0x0020 | 0x0008, out var hToken))
        {
            return;
        }

        try
        {
            if (!LookupPrivilegeValue(null, privilegeName, out var luid))
            {
                return;
            }

            var tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privileges = new LUID_AND_ATTRIBUTES
                {
                    Luid = luid,
                    Attributes = SEPRIVILEGEENABLED
                },
            };

            AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }
        finally
        {
            CloseHandle(hToken);
        }
    }

    private static int StartProcessInSessionInternal(string applicationPath, string arguments, int sessionId, bool hideWindow, bool waitForExit, TimeSpan? timeout)
    {
        if (!WTSQueryUserToken(sessionId, out var impersonationToken))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "WTSQueryUserToken failed.");
        }

        try
        {
            if (!DuplicateTokenEx(
                    impersonationToken,
                    0x02000000 | 0x000F0000 | 0x00000008 | 0x00000002 | 0x00000001,
                    IntPtr.Zero,
                    2,
                    1,
                    out var primaryToken))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "DuplicateTokenEx failed.");
            }

            try
            {
                if (!CreateEnvironmentBlock(out var environment, primaryToken, false))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateEnvironmentBlock failed.");
                }

                try
                {
                    var startupInfo = new STARTUPINFO
                    {
                        Cb = Marshal.SizeOf<STARTUPINFO>(),
                        LpDesktop = @"winsta0\default",
                        DwFlags = hideWindow ? 0x00000001 : 0,
                        WShowWindow = hideWindow ? (short)0 : (short)1,
                    };

                    var commandLine = string.IsNullOrWhiteSpace(arguments)
                        ? $"\"{applicationPath}\""
                        : $"\"{applicationPath}\" {arguments}";

                    if (!CreateProcessAsUser(
                            primaryToken,
                            null,
                            commandLine,
                            IntPtr.Zero,
                            IntPtr.Zero,
                            false,
                            0x00000400 | 0x00000010 | (hideWindow ? 0x08000000 : 0),
                            environment,
                            Path.GetDirectoryName(applicationPath),
                            ref startupInfo,
                            out var processInformation))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcessAsUser failed.");
                    }

                    try
                    {
                        if (!waitForExit)
                        {
                            return 0;
                        }

                        var waitMilliseconds = timeout is null
                            ? Timeout.Infinite
                            : checked((int)Math.Clamp(timeout.Value.TotalMilliseconds, 1, int.MaxValue));

                        var waitResult = WaitForSingleObject(processInformation.HProcess, waitMilliseconds);
                        if (waitResult == WAITTIMEOUT)
                        {
                            throw new TimeoutException($"Process '{applicationPath}' did not finish within {timeout}.");
                        }

                        if (!GetExitCodeProcess(processInformation.HProcess, out var exitCode))
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetExitCodeProcess failed.");
                        }

                        return unchecked((int)exitCode);
                    }
                    finally
                    {
                        CloseHandle(processInformation.HThread);
                        CloseHandle(processInformation.HProcess);
                    }
                }
                finally
                {
                    DestroyEnvironmentBlock(environment);
                }
            }
            finally
            {
                CloseHandle(primaryToken);
            }
        }
        finally
        {
            CloseHandle(impersonationToken);
        }
    }

    public static void StartShellOpenInSession(string targetPath, int sessionId)
    {
        if (Directory.Exists(targetPath))
        {
            StartProcessInSession(Path.Combine(Environment.SystemDirectory, "explorer.exe"), QuoteArgument(targetPath), sessionId);
            return;
        }

        if (File.Exists(targetPath))
        {
            StartProcessInSession(
                Path.Combine(Environment.SystemDirectory, "rundll32.exe"),
                $"shell32.dll,ShellExec_RunDLL {QuoteArgument(targetPath)}",
                sessionId);
            return;
        }

        throw new FileNotFoundException("Entry not found.", targetPath);
    }

    public static void StartCmdScriptInSession(string scriptPath, int sessionId)
    {
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("Script not found.", scriptPath);
        }

        StartProcessInSession(
            Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            $"/c {QuoteArgument(scriptPath)}",
            sessionId,
            hideWindow: true);
    }

    public static void StartCmdScriptAsAdministrator(string scriptPath)
    {
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("Script not found.", scriptPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            Arguments = $"/c {QuoteArgument(scriptPath)}",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(scriptPath),
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start command process.");
    }

    private static string QuoteArgument(string value)
        => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int Cb;
        public string? LpReserved;
        public string? LpDesktop;
        public string? LpTitle;
        public int DwX;
        public int DwY;
        public int DwXSize;
        public int DwYSize;
        public int DwXCountChars;
        public int DwYCountChars;
        public int DwFillAttribute;
        public int DwFlags;
        public short WShowWindow;
        public short CbReserved2;
        public IntPtr LpReserved2;
        public IntPtr HStdInput;
        public IntPtr HStdOutput;
        public IntPtr HStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr HProcess;
        public IntPtr HThread;
        public int DwProcessId;
        public int DwThreadId;
    }

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("Wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(int sessionId, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        IntPtr existingTokenHandle,
        int desiredAccess,
        IntPtr tokenAttributes,
        int impersonationLevel,
        int tokenType,
        out IntPtr duplicateTokenHandle);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out IntPtr environment, IntPtr token, bool inherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool DestroyEnvironmentBlock(IntPtr environment);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessAsUser(
        IntPtr token,
        string? applicationName,
        string commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        bool inheritHandles,
        int creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref STARTUPINFO startupInfo,
        out PROCESS_INFORMATION processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, int milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint dwDesiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr tokenHandle,
        bool disableAllPrivileges,
        ref TOKEN_PRIVILEGES newState,
        int bufferLength,
        IntPtr previousState,
        IntPtr returnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID_AND_ATTRIBUTES Privileges;
    }

    private const uint SEPRIVILEGEENABLED = 0x00000002;
    private const uint WAITTIMEOUT = 0x00000102;
}
