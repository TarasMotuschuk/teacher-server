using Teacher.Common.Contracts;

namespace TeacherClient.CrossPlatform.Models;

public sealed record FrequentProgramEntry(
    string Id,
    string DisplayName,
    string CommandText,
    RemoteCommandRunAs RunAs)
{
    public static FrequentProgramEntry Create(string displayName, string commandText, RemoteCommandRunAs runAs)
        => new(Guid.NewGuid().ToString("N"), displayName.Trim(), commandText.Trim(), runAs);
}
