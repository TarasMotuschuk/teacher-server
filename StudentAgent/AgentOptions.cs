namespace StudentAgent;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public int Port { get; set; } = 5055;

    public string SharedSecret { get; set; } = "change-this-secret";

    public string VisibleBannerText { get; set; } = "Teacher monitoring enabled";
}
