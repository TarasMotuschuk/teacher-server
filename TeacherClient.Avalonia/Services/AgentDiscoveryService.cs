using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Teacher.Common.Contracts;

namespace TeacherClient.CrossPlatform.Services;

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
                    RespondingAddress = result.RemoteEndPoint.Address.ToString(),
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
}
