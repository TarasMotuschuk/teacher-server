namespace Teacher.Common.Contracts;

public sealed record RegistryKeyDto(string Name, string Path, bool HasChildren);

public sealed record RegistryValueDto(string Name, string TypeDisplay, string DataDisplay);

public sealed record RegistryValueEditDto(string Name, string TypeDisplay, string DataDisplay, string RawType, string RawData);

public sealed record SetRegistryValueRequest(string Path, string Name, string Type, string Data);

public sealed record DeleteRegistryValueRequest(string Path, string Name);

public sealed record CreateRegistryKeyRequest(string ParentPath, string KeyName);

public sealed record DeleteRegistryKeyRequest(string Path);
