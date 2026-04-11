using Teacher.Common.Contracts;
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

    public bool BrowserLockEnabled { get; set; }

    public bool InputLockEnabled { get; set; }

    public InputLockVisualMode InputLockVisualMode { get; set; } = InputLockVisualMode.FullscreenOverlay;

    public int BrowserLockCheckIntervalSeconds { get; set; } = 60;

    public int DesktopIconAutoRestoreMinutes { get; set; } = 30;

    public bool VncEnabled { get; set; }

    public int VncPort { get; set; } = 5901;

    public bool VncViewOnly { get; set; } = true;

    public string VncPassword { get; set; } = string.Empty;
}
