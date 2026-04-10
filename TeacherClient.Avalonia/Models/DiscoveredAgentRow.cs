using Teacher.Common.Contracts;
using TeacherClient.CrossPlatform.Localization;
using TeacherClient.CrossPlatform.Models;

namespace TeacherClient.CrossPlatform;

internal sealed record DiscoveredAgentRow(
    string AgentId,
    string Source,
    string Status,
    string GroupName,
    string MachineName,
    string CurrentUser,
    string RespondingAddress,
    int Port,
    string MacAddressesDisplay,
    string Notes,
    string UpdateStatusBadge,
    string UpdateStatusDetail,
    string Version,
    bool VncEnabled,
    bool VncRunning,
    bool VncViewOnly,
    int VncPort,
    string VncStatusMessage,
    DateTime LastSeenUtc,
    bool IsManual)
{
    public bool BrowserLockEnabled { get; set; }

    public bool InputLockEnabled { get; set; }

    public string LastSeenDisplay => LastSeenUtc == DateTime.MinValue ? string.Empty : LastSeenUtc.ToString("u");

    public static DiscoveredAgentRow FromDto(AgentDiscoveryDto dto)
    {
        return new DiscoveredAgentRow(
            dto.AgentId,
            CrossPlatformText.AutoSource,
            CrossPlatformText.Online,
            string.Empty,
            dto.MachineName,
            NormalizeUserDisplay(dto.CurrentUser, dto.MachineName),
            dto.RespondingAddress,
            dto.Port,
            string.Join(", ", dto.MacAddresses),
            string.Empty,
            string.Empty,
            string.Empty,
            dto.Version,
            false,
            false,
            true,
            0,
            string.Empty,
            dto.LastSeenUtc,
            false);
    }

    public static DiscoveredAgentRow FromManualEntry(ManualAgentEntry entry)
    {
        return new DiscoveredAgentRow(
            entry.Id,
            CrossPlatformText.ManualSource,
            CrossPlatformText.Unknown,
            entry.GroupName,
            entry.DisplayName,
            string.Empty,
            entry.IpAddress,
            entry.Port,
            entry.MacAddress,
            entry.Notes,
            string.Empty,
            string.Empty,
            CrossPlatformText.ManualVersion,
            false,
            false,
            true,
            0,
            string.Empty,
            DateTime.MinValue,
            true);
    }

    private static string NormalizeUserDisplay(string? currentUser, string machineName)
    {
        if (string.IsNullOrWhiteSpace(currentUser))
        {
            return string.Empty;
        }

        var trimmed = currentUser.Trim();
        var accountName = trimmed.Contains('\\', StringComparison.Ordinal)
            ? trimmed[(trimmed.LastIndexOf('\\') + 1)..]
            : trimmed;

        return string.Equals(accountName, $"{machineName}$", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : trimmed;
    }
}
