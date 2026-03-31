namespace Teacher.Common.Contracts;

public sealed record RegistryKeyDto(string Name, string Path, bool HasChildren);

public sealed record RegistryValueDto(string Name, string TypeDisplay, string DataDisplay);
