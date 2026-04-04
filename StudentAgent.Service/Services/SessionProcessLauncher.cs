using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StudentAgent.Service.Services;

internal static class SessionProcessLauncher
{
    public static int GetActiveSessionId()
    {
        var sessionId = WTSGetActiveConsoleSessionId();
        return sessionId == 0xFFFFFFFF ? -1 : unchecked((int)sessionId);
    }

    public static void StartProcessInSession(string applicationPath, string arguments, int sessionId)
        => StartProcessInSession(applicationPath, arguments, sessionId, hideWindow: false);

    public static void StartProcessInSession(string applicationPath, string arguments, int sessionId, bool hideWindow)
        => _ = StartProcessInSessionInternal(applicationPath, arguments, sessionId, hideWindow, waitForExit: false, timeout: null);

    public static int StartProcessInSessionAndWait(string applicationPath, string arguments, int sessionId, bool hideWindow, TimeSpan timeout)
        => StartProcessInSessionInternal(applicationPath, arguments, sessionId, hideWindow, waitForExit: true, timeout);

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
                        cb = Marshal.SizeOf<STARTUPINFO>(),
                        lpDesktop = @"winsta0\default",
                        dwFlags = hideWindow ? 0x00000001 : 0,
                        wShowWindow = hideWindow ? (short)0 : (short)1
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

                        var waitResult = WaitForSingleObject(processInformation.hProcess, waitMilliseconds);
                        if (waitResult == WAIT_TIMEOUT)
                        {
                            throw new TimeoutException($"Process '{applicationPath}' did not finish within {timeout}.");
                        }

                        if (!GetExitCodeProcess(processInformation.hProcess, out var exitCode))
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetExitCodeProcess failed.");
                        }

                        return unchecked((int)exitCode);
                    }
                    finally
                    {
                        CloseHandle(processInformation.hThread);
                        CloseHandle(processInformation.hProcess);
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
            WorkingDirectory = Path.GetDirectoryName(scriptPath)
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start command process.");
    }

    private static string QuoteArgument(string value)
        => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
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

    private const uint WAIT_TIMEOUT = 0x00000102;
}
