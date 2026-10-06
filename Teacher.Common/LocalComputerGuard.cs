using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Teacher.Common;

public static class LocalComputerGuard
{
    public static async Task<bool> IsLocalAsync(string host, string? reportedMachineName = null, CancellationToken cancellationToken = default)
    {
        host = host.Trim().Trim('[', ']').TrimEnd('.');
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, Environment.MachineName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(reportedMachineName, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var localAddresses = NetworkInterface.GetAllNetworkInterfaces()
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Select(address => Normalize(address.Address)).ToHashSet();
        bool IsLocalAddress(IPAddress address) => IPAddress.IsLoopback(Normalize(address)) || localAddresses.Contains(Normalize(address));
        if (IPAddress.TryParse(host, out var parsed))
        {
            return IsLocalAddress(parsed);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, timeout.Token);
            return addresses.Any(IsLocalAddress);
        }
        catch (SocketException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static IPAddress Normalize(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
