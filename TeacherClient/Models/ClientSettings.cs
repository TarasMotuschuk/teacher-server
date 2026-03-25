#nullable enable

namespace TeacherClient.Models;

public sealed record ClientSettings(string SharedSecret)
{
    public static ClientSettings Default { get; } = new("change-this-secret");
}
