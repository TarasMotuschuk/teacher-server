namespace Teacher.Common.Contracts;

public sealed record WebRtcIceCandidateDto(
    string SessionId,
    string Candidate,
    string? SdpMid,
    int? SdpMLineIndex);
