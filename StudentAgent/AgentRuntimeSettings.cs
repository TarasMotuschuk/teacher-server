using Teacher.Common.Localization;

namespace StudentAgent;

public sealed class AgentRuntimeSettings
{
    public int Port { get; set; } = 5055;

    public int DiscoveryPort { get; set; } = 5056;

    public string SharedSecret { get; set; } = "change-this-secret";

    public string AdminPasswordHash { get; set; } = string.Empty;

    public string VisibleBannerText { get; set; } = "Teacher monitoring enabled";

    public UiLanguage Language { get; set; } = UiLanguageExtensions.GetDefault();
}
