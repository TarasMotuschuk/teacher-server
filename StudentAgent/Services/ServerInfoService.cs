using System.Runtime.InteropServices;
using Teacher.Common.Contracts;

namespace StudentAgent.Services;

public sealed class ServerInfoService
{
    public ServerInfoDto GetInfo()
    {
        return new ServerInfoDto(
            Environment.MachineName,
            Environment.UserName,
            RuntimeInformation.OSDescription,
            DateTime.UtcNow,
            true);
    }
}
