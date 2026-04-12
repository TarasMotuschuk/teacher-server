using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using StudentAgent.Services;
using Teacher.Common.Contracts;

namespace StudentAgent.Service.Services;

public sealed class WindowsRestrictionsService
{
    private const string ExplorerPoliciesPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer";
    private const string SystemPoliciesPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
    private readonly AgentLogService _agentLogService;

    public WindowsRestrictionsService(AgentLogService agentLogService)
    {
        _agentLogService = agentLogService;
    }

    public void SetRestriction(WindowsRestrictionKind restriction, bool enabled)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows restrictions are only supported on Windows student agents.");
        }

        var definition = GetDefinition(restriction);
        using var machineKey = Registry.LocalMachine.CreateSubKey(definition.RegistryPath, writable: true)
            ?? throw new InvalidOperationException($"Cannot open or create registry path '{definition.RegistryPath}'.");

        if (enabled)
        {
            machineKey.SetValue(definition.ValueName, 1, RegistryValueKind.DWord);
            if (restriction == WindowsRestrictionKind.TaskManager)
            {
                // Win10/11 Shell (incl. taskbar "Task Manager") often evaluates per-user policy (HKCU).
                // HKLM alone is not always enough until logoff; mirror into loaded HKEY_USERS\<SID> hives.
                ApplyDisableTaskMgrToLoadedUserHives(enabled: true);
                CloseRunningTaskManager();
            }
        }
        else
        {
            machineKey.DeleteValue(definition.ValueName, throwOnMissingValue: false);
            if (restriction == WindowsRestrictionKind.TaskManager)
            {
                ApplyDisableTaskMgrToLoadedUserHives(enabled: false);
            }
        }

        BroadcastPolicyRefresh();
        _agentLogService.LogInfo(
            $"Windows restriction '{restriction}' {(enabled ? "enabled" : "disabled")} via teacher command.");
    }

    private static RestrictionDefinition GetDefinition(WindowsRestrictionKind restriction)
        => restriction switch
        {
            WindowsRestrictionKind.TaskManager => new(SystemPoliciesPath, "DisableTaskMgr"),
            WindowsRestrictionKind.RunDialog => new(ExplorerPoliciesPath, "NoRun"),
            WindowsRestrictionKind.ControlPanelAndSettings => new(ExplorerPoliciesPath, "NoControlPanel"),
            WindowsRestrictionKind.LockWorkstation => new(SystemPoliciesPath, "DisableLockWorkstation"),
            WindowsRestrictionKind.ChangePassword => new(SystemPoliciesPath, "DisableChangePassword"),
            _ => throw new ArgumentOutOfRangeException(nameof(restriction), restriction, "Unsupported Windows restriction."),
        };

    private static bool ShouldSkipUserHiveSid(string sid)
    {
        if (string.IsNullOrEmpty(sid) || sid.StartsWith('.'))
        {
            return true;
        }

        // Merged view / COM (not an interactive user profile hive).
        if (sid.EndsWith("_Classes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Service / system accounts (no Explorer taskbar).
        if (sid is "S-1-5-18" or "S-1-5-19" or "S-1-5-20")
        {
            return true;
        }

        return false;
    }

    private static void BroadcastPolicyRefresh()
    {
        var payload = Marshal.StringToHGlobalUni("Policy");
        try
        {
            _ = SendMessageTimeout(
                new IntPtr(0xFFFF),
                0x001A,
                IntPtr.Zero,
                payload,
                0x0002,
                5000,
                out _);
        }
        finally
        {
            Marshal.FreeHGlobal(payload);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    private void ApplyDisableTaskMgrToLoadedUserHives(bool enabled)
    {
        const string relativePoliciesPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";

        foreach (var sid in Registry.Users.GetSubKeyNames())
        {
            if (ShouldSkipUserHiveSid(sid))
            {
                continue;
            }

            try
            {
                using var userKey = Registry.Users.CreateSubKey($@"{sid}\{relativePoliciesPath}", writable: true);
                if (userKey is null)
                {
                    continue;
                }

                if (enabled)
                {
                    userKey.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                }
                else
                {
                    userKey.DeleteValue("DisableTaskMgr", throwOnMissingValue: false);
                }
            }
            catch (Exception ex)
            {
                _agentLogService.LogWarning($"DisableTaskMgr in HKU\\{sid}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Stops any running Task Manager so the new policy applies immediately.
    /// Uses <c>taskkill.exe</c> after <see cref="System.Diagnostics.Process.Kill()"/> so termination works from Session 0
    /// and is not missed due to WOW64 process-enumeration limits; failures are logged (previously swallowed).
    /// </summary>
    private void CloseRunningTaskManager()
    {
        foreach (var process in Process.GetProcessesByName("Taskmgr"))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(milliseconds: 5000);
            }
            catch (Exception ex)
            {
                _agentLogService.LogWarning($"Process.Kill(Taskmgr PID {process.Id}) failed: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }

        var taskkillPath = Path.Combine(Environment.SystemDirectory, "taskkill.exe");
        if (!File.Exists(taskkillPath))
        {
            _agentLogService.LogWarning($"Cannot close Task Manager: '{taskkillPath}' was not found.");
            return;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = taskkillPath,
                Arguments = "/F /IM Taskmgr.exe /T",
                CreateNoWindow = true,
                UseShellExecute = false,
            });

            if (process is null)
            {
                _agentLogService.LogWarning("Could not start taskkill.exe to close Task Manager.");
                return;
            }

            using (process)
            {
                if (!process.WaitForExit(15000))
                {
                    _agentLogService.LogWarning("taskkill did not finish within 15s while closing Task Manager.");
                    return;
                }

                // 0 = terminated; 128 = no matching process (nothing to do). Other codes are unexpected.
                if (process.ExitCode is not (0 or 128))
                {
                    _agentLogService.LogWarning($"taskkill /IM Taskmgr.exe exited with code {process.ExitCode}.");
                }
            }
        }
        catch (Exception ex)
        {
            _agentLogService.LogWarning($"taskkill failed while closing Task Manager: {ex.Message}");
        }
    }

    private sealed record RestrictionDefinition(string RegistryPath, string ValueName);
}
