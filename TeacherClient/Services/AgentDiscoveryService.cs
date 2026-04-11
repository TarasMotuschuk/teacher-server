using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Teacher.Common.Contracts;

namespace TeacherClient.Services;

public sealed class AgentDiscoveryService
{
    private const string DiscoveryRequestMessage = "TEACHER_SERVER_DISCOVERY_V1";
    private const int DefaultDiscoveryPort = 5056;

    public async Task<IReadOnlyList<AgentDiscoveryDto>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        using var udpClient = new UdpClient(0)
        {
            EnableBroadcast = true,
        };

        var requestBytes = Encoding.UTF8.GetBytes(DiscoveryRequestMessage);
        await udpClient.SendAsync(requestBytes, requestBytes.Length, new IPEndPoint(IPAddress.Broadcast, DefaultDiscoveryPort));

        var deadline = DateTime.UtcNow.AddMilliseconds(1200);
        var agents = new Dictionary<string, AgentDiscoveryDto>(StringComparer.OrdinalIgnoreCase);

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var receiveTask = udpClient.ReceiveAsync(cancellationToken).AsTask();
            var completedTask = await Task.WhenAny(receiveTask, Task.Delay(remaining, cancellationToken));
            if (completedTask != receiveTask)
            {
                break;
            }

            try
            {
                var result = await receiveTask;
                var json = Encoding.UTF8.GetString(result.Buffer);
                var parsed = JsonSerializer.Deserialize<AgentDiscoveryDto>(json);
                if (parsed is null)
                {
                    continue;
                }

                var normalized = parsed with
                {
                    RespondingAddress = SelectPreferredRespondingAddress(parsed, result.RemoteEndPoint.Address),
                    LastSeenUtc = DateTime.UtcNow,
                };

                agents[normalized.AgentId] = normalized;
            }
            catch
            {
            }
        }

        return agents.Values
            .OrderBy(x => x.MachineName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string SelectPreferredRespondingAddress(AgentDiscoveryDto parsed, IPAddress remoteEndPointAddress)
    {
        var localNetworks = GetLocalIpv4Networks();
        foreach (var candidate in parsed.IpAddresses)
        {
            if (!IPAddress.TryParse(candidate, out var candidateAddress) ||
                candidateAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                continue;
            }

            if (localNetworks.Any(local => AreOnSameSubnet(local.Address, local.PrefixMask, candidateAddress)))
            {
                return candidate;
            }
        }

        return remoteEndPointAddress.ToString();
    }

    private static IReadOnlyList<(IPAddress Address, IPAddress PrefixMask)> GetLocalIpv4Networks()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Where(unicast =>
                unicast.Address.AddressFamily == AddressFamily.InterNetwork &&
                !IPAddress.IsLoopback(unicast.Address) &&
                unicast.IPv4Mask is not null)
            .Select(unicast => (unicast.Address, unicast.IPv4Mask!))
            .ToList();
    }

    private static bool AreOnSameSubnet(IPAddress localAddress, IPAddress prefixMask, IPAddress candidateAddress)
    {
        var localBytes = localAddress.GetAddressBytes();
        var maskBytes = prefixMask.GetAddressBytes();
        var candidateBytes = candidateAddress.GetAddressBytes();
        for (var index = 0; index < localBytes.Length; index++)
        {
            if ((localBytes[index] & maskBytes[index]) != (candidateBytes[index] & maskBytes[index]))
            {
                return false;
            }
        }

        return true;
    }
}
