namespace Teacher.Common.Contracts;

public sealed record RemoteCommandRequest(
    string Script,
    RemoteCommandRunAs RunAs);
