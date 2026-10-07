using System.Text.Json.Serialization;
using Teacher.Common.Contracts;
using TeacherClient.CrossPlatform.Localization;

namespace TeacherClient.CrossPlatform.Models;

public sealed record FrequentProgramEntry(
    string Id,
    string DisplayName,
    string CommandText,
    RemoteCommandRunAs RunAs)
{
    [JsonIgnore]
    public string RunAsDisplay => RunAs == RemoteCommandRunAs.Administrator
        ? CrossPlatformText.RunAsAdministrator
        : CrossPlatformText.RunAsCurrentUser;

    public static FrequentProgramEntry Create(string displayName, string commandText, RemoteCommandRunAs runAs)
        => new(Guid.NewGuid().ToString("N"), displayName.Trim(), commandText.Trim(), runAs);
}
