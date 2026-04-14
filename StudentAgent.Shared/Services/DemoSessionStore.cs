using System.Collections.Concurrent;
using Teacher.Common.Contracts;

namespace StudentAgent.Services;

public sealed class DemoSessionStore
{
    private sealed record DemoSessionState(
        string SessionId,
        DateTime StartedUtc,
        bool FullscreenLock,
        string? OfferSdpType,
        string? OfferSdp,
        string? AnswerSdpType,
        string? AnswerSdp,
        ConcurrentQueue<WebRtcIceCandidateDto> PendingTeacherCandidates,
        ConcurrentQueue<WebRtcIceCandidateDto> PendingStudentCandidates);

    private readonly ConcurrentDictionary<string, DemoSessionState> _sessions = new(StringComparer.Ordinal);
    private volatile string? _activeSessionId;

    public DemoSessionStatusDto GetStatus()
    {
        var sessionId = _activeSessionId;
        if (string.IsNullOrWhiteSpace(sessionId) || !_sessions.TryGetValue(sessionId, out var state))
        {
            return new DemoSessionStatusDto(false, null, null, FullscreenLock: false);
        }

        return new DemoSessionStatusDto(true, state.SessionId, state.StartedUtc, state.FullscreenLock);
    }

    public void StartOrReplace(string sessionId, bool fullscreenLock, string? offerSdpType = null, string? offerSdp = null)
    {
        var state = new DemoSessionState(
            sessionId,
            DateTime.UtcNow,
            fullscreenLock,
            offerSdpType,
            offerSdp,
            AnswerSdpType: null,
            AnswerSdp: null,
            new ConcurrentQueue<WebRtcIceCandidateDto>(),
            new ConcurrentQueue<WebRtcIceCandidateDto>());

        _sessions[sessionId] = state;
        _activeSessionId = sessionId;
    }

    public void Stop(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
        if (string.Equals(_activeSessionId, sessionId, StringComparison.Ordinal))
        {
            _activeSessionId = null;
        }
    }

    public (string SdpType, string Sdp)? TryConsumeOffer(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var state) || string.IsNullOrWhiteSpace(state.OfferSdpType) || string.IsNullOrWhiteSpace(state.OfferSdp))
        {
            return null;
        }

        var next = state with { OfferSdpType = null, OfferSdp = null };
        _sessions[sessionId] = next;
        return (state.OfferSdpType!, state.OfferSdp!);
    }

    public void SetAnswer(string sessionId, string sdpType, string sdp)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
        {
            return;
        }

        _sessions[sessionId] = state with { AnswerSdpType = sdpType, AnswerSdp = sdp };
    }

    public (string SdpType, string Sdp)? TryConsumeAnswer(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var state) || string.IsNullOrWhiteSpace(state.AnswerSdpType) || string.IsNullOrWhiteSpace(state.AnswerSdp))
        {
            return null;
        }

        var next = state with { AnswerSdpType = null, AnswerSdp = null };
        _sessions[sessionId] = next;
        return (state.AnswerSdpType!, state.AnswerSdp!);
    }

    public void EnqueueTeacherIce(WebRtcIceCandidateDto candidate)
    {
        if (!_sessions.TryGetValue(candidate.SessionId, out var state))
        {
            return;
        }

        state.PendingTeacherCandidates.Enqueue(candidate);
    }

    public IReadOnlyList<WebRtcIceCandidateDto> DrainTeacherIce(string sessionId, int maxItems = 128)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
        {
            return Array.Empty<WebRtcIceCandidateDto>();
        }

        var drained = new List<WebRtcIceCandidateDto>();
        while (drained.Count < maxItems && state.PendingTeacherCandidates.TryDequeue(out var item))
        {
            drained.Add(item);
        }

        return drained;
    }

    public void EnqueueStudentIce(WebRtcIceCandidateDto candidate)
    {
        if (!_sessions.TryGetValue(candidate.SessionId, out var state))
        {
            return;
        }

        state.PendingStudentCandidates.Enqueue(candidate);
    }

    public IReadOnlyList<WebRtcIceCandidateDto> DrainStudentIce(string sessionId, int maxItems = 128)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
        {
            return Array.Empty<WebRtcIceCandidateDto>();
        }

        var drained = new List<WebRtcIceCandidateDto>();
        while (drained.Count < maxItems && state.PendingStudentCandidates.TryDequeue(out var item))
        {
            drained.Add(item);
        }

        return drained;
    }
}
