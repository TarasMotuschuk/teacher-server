using Teacher.Common.Localization;

namespace StudentAgent;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public int Port { get; set; } = 5055;

    public int DiscoveryPort { get; set; } = 5056;

    public string SharedSecret { get; set; } = "change-this-secret";

    public string AdminPasswordHash { get; set; } = "8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918";

    public string VisibleBannerText { get; set; } = "Teacher monitoring enabled";

    public UiLanguage Language { get; set; } = UiLanguageExtensions.GetDefault();
}
