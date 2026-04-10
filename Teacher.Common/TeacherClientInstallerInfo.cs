namespace Teacher.Common;

public sealed record TeacherClientInstallerInfo(
    string Version,
    string LocalInstallerPath,
    string PlatformLabel,
    DateTime DownloadedAtUtc);
