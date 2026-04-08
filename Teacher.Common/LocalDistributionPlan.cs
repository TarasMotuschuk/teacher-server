namespace Teacher.Common;

public sealed record LocalDistributionPlan(
    string EntryName,
    string DestinationRoot,
    IReadOnlyList<string> DirectoriesToEnsure,
    IReadOnlyList<LocalDistributionFile> Files);
