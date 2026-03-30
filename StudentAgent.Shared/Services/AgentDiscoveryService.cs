using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Teacher.Common.Contracts;

namespace StudentAgent.Services;

public sealed class AgentDiscoveryService : BackgroundService
{
    private const string DiscoveryRequestMessage = "TEACHER_SERVER_DISCOVERY_V1";
    private readonly AgentSettingsStore _settingsStore;
    private readonly ServerInfoService _serverInfoService;
    private readonly NetworkIdentityService _networkIdentityService;
    private readonly AgentLogService _logService;

    public AgentDiscoveryService(
        AgentSettingsStore settingsStore,
        ServerInfoService serverInfoService,
        NetworkIdentityService networkIdentityService,
        AgentLogService logService)
    {
        _settingsStore = settingsStore;
        _serverInfoService = serverInfoService;
        _networkIdentityService = networkIdentityService;
        _logService = logService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var discoveryPort = _settingsStore.Current.DiscoveryPort;
        using var udpClient = new UdpClient(discoveryPort)
        {
            EnableBroadcast = true
        };

        _logService.LogInfo($"UDP discovery responder listening on port {discoveryPort}.");

        while (!stoppingToken.IsCancellationRequested)
        {
            UdpReceiveResult received;

            try
            {
                received = await udpClient.ReceiveAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logService.LogError($"UDP discovery receive failed: {ex}");
                continue;
            }

            var payload = Encoding.UTF8.GetString(received.Buffer);
            if (!string.Equals(payload, DiscoveryRequestMessage, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                var settings = _settingsStore.Current;
                var info = _serverInfoService.GetInfo();
                var ipAddresses = _networkIdentityService.GetIPv4Addresses();
                var macAddresses = _networkIdentityService.GetMacAddresses();
                var response = new AgentDiscoveryDto(
                    Environment.MachineName,
                    info.MachineName,
                    info.CurrentUser,
                    info.OsDescription,
                    typeof(AgentDiscoveryService).Assembly.GetName().Version?.ToString() ?? "1.0.0",
                    settings.Port,
                    settings.DiscoveryPort,
                    info.IsVisibleModeEnabled,
                    ipAddresses.FirstOrDefault() ?? received.RemoteEndPoint.Address.ToString(),
                    ipAddresses,
                    macAddresses,
                    DateTime.UtcNow);

                var responseBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));
                await udpClient.SendAsync(responseBytes, responseBytes.Length, received.RemoteEndPoint);
                _logService.LogInfo($"UDP discovery response sent to {received.RemoteEndPoint.Address}:{received.RemoteEndPoint.Port}.");
            }
            catch (Exception ex)
            {
                _logService.LogError($"UDP discovery response failed: {ex}");
            }
        }
    }
}
