namespace Teacher.Common.Contracts;

public sealed record AgentDiscoveryDto(
    string AgentId,
    string MachineName,
    string CurrentUser,
    string OsDescription,
    string Version,
    int Port,
    int DiscoveryPort,
    bool IsVisibleModeEnabled,
    string RespondingAddress,
    IReadOnlyList<string> IpAddresses,
    IReadOnlyList<string> MacAddresses,
    DateTime LastSeenUtc);
