using System.Runtime.InteropServices;
using Teacher.Common.Contracts;

namespace StudentAgent.Services;

public sealed class ServerInfoService
{
    private readonly AgentSettingsStore _settingsStore;

    public ServerInfoService(AgentSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public ServerInfoDto GetInfo()
    {
        var machineName = Environment.MachineName;
        var currentUser = Environment.UserName;
        var osDescription = RuntimeInformation.OSDescription;

        return new ServerInfoDto(
            machineName,
            currentUser,
            osDescription,
            DateTime.UtcNow,
            true,
            _settingsStore.Current.BrowserLockEnabled,
            _settingsStore.Current.InputLockEnabled);
    }
}
