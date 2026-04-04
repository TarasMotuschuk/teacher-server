namespace Teacher.Common.Contracts;

public sealed record VncStateDto(
    bool Enabled,
    bool Running,
    int Port,
    bool ViewOnly,
    string? Message);

public sealed record StartVncRequest(
    int? Port = null,
    bool? ViewOnly = null,
    string? Password = null);

public sealed record StopVncRequest;
