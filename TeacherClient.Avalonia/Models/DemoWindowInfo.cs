namespace TeacherClient.CrossPlatform.Models;

public sealed record DemoWindowInfo(
    long PlatformWindowId,
    string Title,
    string? OwnerName = null);

