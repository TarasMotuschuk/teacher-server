namespace Teacher.Common.Contracts;

public sealed record VncStateDto(
    bool Enabled,
    bool Running,
    int Port,
    bool ViewOnly,
    string? Message);
