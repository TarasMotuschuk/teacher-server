namespace Teacher.Common.Contracts;

public sealed record StartVncRequest(
    int? Port = null,
    bool? ViewOnly = null,
    string? Password = null);
