namespace Teacher.Common.Contracts;

public sealed record SetRegistryValueRequest(string Path, string Name, string Type, string Data);
