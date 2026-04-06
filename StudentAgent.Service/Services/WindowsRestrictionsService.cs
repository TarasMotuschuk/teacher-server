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
                CloseRunningTaskManagerWindows();
            }
        }
        else
        {
            machineKey.DeleteValue(definition.ValueName, throwOnMissingValue: false);
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
            WindowsRestrictionKind.LogOff => new(ExplorerPoliciesPath, "NoLogoff"),
            _ => throw new ArgumentOutOfRangeException(nameof(restriction), restriction, "Unsupported Windows restriction.")
        };

    private static void CloseRunningTaskManagerWindows()
    {
        foreach (var process in Process.GetProcessesByName("Taskmgr"))
        {
            try
            {
                using (process)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        }
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

    private sealed record RestrictionDefinition(string RegistryPath, string ValueName);
}
