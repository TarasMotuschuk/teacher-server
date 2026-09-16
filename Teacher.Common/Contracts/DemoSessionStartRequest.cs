namespace Teacher.Common.Contracts;

public sealed record DemoSessionStartRequest(
    string SessionId,
    string SdpType,
    string Sdp,
    bool IncludeAudio,
    bool AudioMutedByDefault,
    bool FullscreenLock);
