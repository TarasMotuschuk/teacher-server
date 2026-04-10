namespace Teacher.Common;

internal sealed record TeacherClientUpdateManifest(
    string Version,
    string? WindowsMsiUrl,
    string? WindowsMsiSha256,
    string? MacPkgUrl,
    string? MacPkgSha256);
