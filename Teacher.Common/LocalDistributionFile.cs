namespace Teacher.Common;

public sealed record LocalDistributionFile(
    string LocalPath,
    string RemoteDirectory,
    string DisplayPath);
