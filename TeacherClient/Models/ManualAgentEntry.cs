namespace TeacherClient.Models;

public sealed class ManualAgentEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string DisplayName { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public int Port { get; set; } = 5055;

    public string MacAddress { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;
}
