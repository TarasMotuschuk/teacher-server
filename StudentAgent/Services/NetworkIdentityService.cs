using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace StudentAgent.Services;

public sealed class NetworkIdentityService
{
    public IReadOnlyList<string> GetMacAddresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(IsEligibleInterface)
            .Select(x => x.GetPhysicalAddress()?.ToString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(FormatMacAddress)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetIPv4Addresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(IsEligibleInterface)
            .SelectMany(x => x.GetIPProperties().UnicastAddresses)
            .Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(x.Address))
            .Select(x => x.Address.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsEligibleInterface(NetworkInterface networkInterface)
    {
        return networkInterface.OperationalStatus == OperationalStatus.Up &&
               networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
               networkInterface.NetworkInterfaceType != NetworkInterfaceType.Tunnel;
    }

    private static string FormatMacAddress(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        return string.Join(":", Enumerable.Range(0, raw.Length / 2).Select(i => raw.Substring(i * 2, 2)));
    }
}
