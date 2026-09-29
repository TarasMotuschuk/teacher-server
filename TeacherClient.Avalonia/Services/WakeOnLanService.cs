using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace TeacherClient.CrossPlatform.Services;

internal static partial class WakeOnLanService
{
    private const int PacketRepeatCount = 2;
    private static readonly int[] WolPorts = [9, 7];

    public static IReadOnlyList<byte[]> ParseMacAddresses(string? macAddressesDisplay)
    {
        if (string.IsNullOrWhiteSpace(macAddressesDisplay))
        {
            return [];
        }

        var results = new List<byte[]>();
        foreach (var part in macAddressesDisplay.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryParseMacAddress(part, out var mac))
            {
                results.Add(mac);
            }
        }

        return results;
    }

    public static bool TryParseMacAddress(string? value, out byte[] macBytes)
    {
        macBytes = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var hex = MacHexRegex().Replace(value.Trim(), string.Empty);
        if (hex.Length != 12)
        {
            return false;
        }

        var bytes = new byte[6];
        for (var i = 0; i < 6; i++)
        {
            if (!byte.TryParse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[i]))
            {
                return false;
            }
        }

        // Reject all-zero / broadcast-ish MACs that are never useful for WoL targets.
        if (bytes.All(b => b == 0x00) || bytes.All(b => b == 0xFF))
        {
            return false;
        }

        macBytes = bytes;
        return true;
    }

    public static async Task SendMagicPacketsAsync(
        IEnumerable<byte[]> macAddresses,
        string? hostAddressHint = null,
        CancellationToken cancellationToken = default)
    {
        var targets = BuildBroadcastTargets(hostAddressHint);
        var packetMacs = macAddresses.ToList();
        if (packetMacs.Count == 0 || targets.Count == 0)
        {
            return;
        }

        using var client = new UdpClient();
        client.EnableBroadcast = true;

        foreach (var mac in packetMacs)
        {
            var packet = BuildMagicPacket(mac);
            foreach (var endpoint in targets)
            {
                for (var i = 0; i < PacketRepeatCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await client.SendAsync(packet, endpoint, cancellationToken);
                }
            }
        }
    }

    private static List<IPEndPoint> BuildBroadcastTargets(string? hostAddressHint)
    {
        var endpoints = new List<IPEndPoint>();
        foreach (var port in WolPorts)
        {
            endpoints.Add(new IPEndPoint(IPAddress.Broadcast, port));
        }

        if (IPAddress.TryParse(hostAddressHint, out var host)
            && host.AddressFamily == AddressFamily.InterNetwork
            && !IPAddress.IsLoopback(host)
            && !host.Equals(IPAddress.Any)
            && !host.Equals(IPAddress.Broadcast))
        {
            var bytes = host.GetAddressBytes();
            bytes[3] = 0xFF;
            var directed = new IPAddress(bytes);
            foreach (var port in WolPorts)
            {
                endpoints.Add(new IPEndPoint(directed, port));
                endpoints.Add(new IPEndPoint(host, port));
            }
        }

        return endpoints;
    }

    private static byte[] BuildMagicPacket(byte[] mac)
    {
        var packet = new byte[6 + (16 * 6)];
        for (var i = 0; i < 6; i++)
        {
            packet[i] = 0xFF;
        }

        for (var i = 0; i < 16; i++)
        {
            Buffer.BlockCopy(mac, 0, packet, 6 + (i * 6), 6);
        }

        return packet;
    }

    [GeneratedRegex(@"[^0-9A-Fa-f]")]
    private static partial Regex MacHexRegex();
}
