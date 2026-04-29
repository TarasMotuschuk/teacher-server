namespace Teacher.Common.Contracts;

public sealed record DemoSessionStartResponse(
    string SdpType,
    string Sdp);
