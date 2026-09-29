namespace TeacherClient.CrossPlatform.Models;

/// <summary>
/// A previously discovered student PC remembered for Wake-on-LAN after it powers off.
/// </summary>
public sealed class KnownAgentEntry
{
    public string AgentId { get; set; } = string.Empty;

    public string MachineName { get; set; } = string.Empty;

    public string RespondingAddress { get; set; } = string.Empty;

    public int Port { get; set; } = 5055;

    public string MacAddresses { get; set; } = string.Empty;

    public DateTime LastSeenUtc { get; set; }
}
