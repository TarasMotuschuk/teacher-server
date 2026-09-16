using System.Net.Http.Json;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using Teacher.Common.Contracts;

namespace TeacherClient.CrossPlatform.Services;

/// <summary>
/// Streams the teacher screen to student agents over WebRTC.
/// One shared capture + encoder pipeline feeds every connected student, so classroom-wide
/// demonstrations cost the same CPU as a single-student stream.
/// </summary>
public sealed class DemoWebRtcTeacherStreamer : IDisposable
{
    // Short timeout: all calls target student agents on the local network. The default
    // 100 s HttpClient timeout would freeze start/stop when an agent is busy or gone.
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly object _sync = new();
    private readonly Dictionary<string, StudentConnection> _students = new(StringComparer.Ordinal);
    private readonly DemoDiagnosticLog _diagnosticLog = new(GetTeacherDiagnosticLogPath());
    private readonly DemoVideoSourceFactory _videoSourceFactory = new();

    private IVideoSource? _sharedSource;
    private int _pendingStarts;

    private sealed record StudentConnection(RTCPeerConnection Pc, EncodedSampleDelegate EncodedHandler);

    public async Task StartAsync(
        string studentBaseUrl,
        string sharedSecret,
        string sessionId,
        DemoCaptureTarget? captureTarget = null,
        int captureX = 0,
        int captureY = 0,
        int captureWidth = 1280,
        int captureHeight = 720,
        int captureFps = 15)
    {
        lock (_sync)
        {
            if (_students.ContainsKey(studentBaseUrl))
            {
                _diagnosticLog.LogInfo($"Teacher demo start skipped: connection already exists for {studentBaseUrl}.");
                return;
            }

            // Starts run in parallel for the whole class; keep the shared source alive
            // until every start attempt has either registered a student or failed.
            _pendingStarts++;
        }

        try
        {
            await StartCoreAsync(studentBaseUrl, sharedSecret, sessionId, captureTarget, captureX, captureY, captureWidth, captureHeight, captureFps);
        }
        finally
        {
            lock (_sync)
            {
                _pendingStarts--;
            }

            ReleaseSharedSourceIfUnused();
        }
    }

    private async Task StartCoreAsync(
        string studentBaseUrl,
        string sharedSecret,
        string sessionId,
        DemoCaptureTarget? captureTarget,
        int captureX,
        int captureY,
        int captureWidth,
        int captureHeight,
        int captureFps)
    {

        _diagnosticLog.LogInfo(
            $"Teacher demo start requested: student={studentBaseUrl}, sessionId={sessionId}, capture={captureX},{captureY} {captureWidth}x{captureHeight}@{captureFps}.");

        // Quick connectivity check with a clear error message (helps diagnose wrong IP/port).
        try
        {
            using var healthReq = new HttpRequestMessage(HttpMethod.Get, $"{studentBaseUrl}/health");
            healthReq.Headers.TryAddWithoutValidation("X-Teacher-Secret", sharedSecret);
            using var healthResp = await _httpClient.SendAsync(healthReq);
            healthResp.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _diagnosticLog.LogError($"Teacher demo health check failed for {studentBaseUrl}: {ex}");
            throw new InvalidOperationException($"Cannot reach student agent at {studentBaseUrl}. {ex.Message}", ex);
        }

        // SIPSorceryMedia.Encoders does not ship a macOS libvpx dylib, so macOS teachers
        // encode H.264 via VideoToolbox (see MacOsVideoToolboxH264Encoder). Windows/Linux
        // still use the bundled libvpx VP8 encoder.
        var preferredCodec = OperatingSystem.IsMacOS() ? VideoCodecsEnum.H264 : VideoCodecsEnum.VP8;

        var target = captureTarget ?? new DemoCaptureTarget(
            DemoCaptureTargetKind.Screen,
            captureX,
            captureY,
            Math.Max(16, captureWidth),
            Math.Max(16, captureHeight));

        var source = GetOrCreateSharedSource(target, captureFps, preferredCodec);

        RTCPeerConnection? pc = null;
        EncodedSampleDelegate? encodedHandler = null;
        long localIceCandidates = 0;
        long remoteIceCandidates = 0;

        try
        {
            pc = new RTCPeerConnection(new RTCConfiguration { X_UseRtpFeedbackProfile = true });
            var capturedPc = pc;

            var videoTrack = new MediaStreamTrack(source.GetVideoSourceFormats(), MediaStreamStatusEnum.SendOnly);
            pc.addTrack(videoTrack);

            encodedHandler = (durationRtpUnits, sample) =>
            {
                try
                {
                    capturedPc.SendVideo(durationRtpUnits, sample);
                }
                catch
                {
                    // Peer may be closing; the state-change handler cleans up.
                }
            };
            source.OnVideoSourceEncodedSample += encodedHandler;

            pc.OnVideoFormatsNegotiated += (formats) => source.SetVideoSourceFormat(formats.First());

            pc.onicecandidate += (cand) =>
            {
                if (cand is null)
                {
                    return;
                }

                var count = Interlocked.Increment(ref localIceCandidates);
                if (count == 1 || count % 10 == 0)
                {
                    _diagnosticLog.LogInfo($"Teacher demo local ICE candidates for {studentBaseUrl}: {count}.");
                }

                var dto = new WebRtcIceCandidateDto(sessionId, cand.candidate, cand.sdpMid, cand.sdpMLineIndex);
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{studentBaseUrl}/api/demo/webrtc/ice/teacher")
                {
                    Content = JsonContent.Create(dto),
                };
                req.Headers.TryAddWithoutValidation("X-Teacher-Secret", sharedSecret);
                _ = _httpClient.SendAsync(req);
            };

            pc.onconnectionstatechange += async (state) =>
            {
                _diagnosticLog.LogInfo($"Teacher demo peer state for {studentBaseUrl}: {state}.");
                if (state == RTCPeerConnectionState.connected)
                {
                    // Idempotent for the shared source; late joiners need a fresh keyframe.
                    await source.StartVideo();
                    TryForceKeyFrame(source);
                    _diagnosticLog.LogInfo($"Teacher demo video flowing for {studentBaseUrl} (shared source, keyframe forced).");
                }
                else if (state == RTCPeerConnectionState.closed || state == RTCPeerConnectionState.failed)
                {
                    DetachStudent(studentBaseUrl, capturedPc, $"peer state {state}");
                }
            };

            var offerInit = pc.createOffer();
            await pc.setLocalDescription(offerInit);
            _diagnosticLog.LogInfo($"Teacher demo local offer created for {studentBaseUrl}: type={pc.localDescription.type}.");

            var startReq = new DemoSessionStartRequest(
                sessionId,
                pc.localDescription.type.ToString(),
                pc.localDescription.sdp.ToString(),
                IncludeAudio: false,
                AudioMutedByDefault: true,
                FullscreenLock: true);

            using (var req = new HttpRequestMessage(HttpMethod.Post, $"{studentBaseUrl}/api/demo/webrtc/start") { Content = JsonContent.Create(startReq) })
            {
                req.Headers.TryAddWithoutValidation("X-Teacher-Secret", sharedSecret);
                var resp = await _httpClient.SendAsync(req);
                resp.EnsureSuccessStatusCode();
                _diagnosticLog.LogInfo($"Teacher demo start request accepted by {studentBaseUrl}: HTTP {(int)resp.StatusCode}.");
            }

            // Verify that the student service marked the demo session active (gives immediate feedback if UIHost is not running).
            for (var i = 0; i < 40; i++)
            {
                using var statusReq = new HttpRequestMessage(HttpMethod.Get, $"{studentBaseUrl}/api/demo/status");
                statusReq.Headers.TryAddWithoutValidation("X-Teacher-Secret", sharedSecret);
                using var statusResp = await _httpClient.SendAsync(statusReq);
                statusResp.EnsureSuccessStatusCode();
                var status = await statusResp.Content.ReadFromJsonAsync<DemoSessionStatusDto>();
                if (status is not null && status.Active && string.Equals(status.SessionId, sessionId, StringComparison.Ordinal))
                {
                    _diagnosticLog.LogInfo($"Teacher demo session became active on {studentBaseUrl}: startedUtc={status.StartedUtc:O}.");
                    break;
                }

                await Task.Delay(100);
                if (i == 39)
                {
                    throw new InvalidOperationException("Student demo session did not become active. Ensure StudentAgent.UIHost is running in the student session.");
                }
            }

            // Poll answer until available.
            for (var i = 0; i < 80; i++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{studentBaseUrl}/api/demo/webrtc/answer?sessionId={Uri.EscapeDataString(sessionId)}");
                req.Headers.TryAddWithoutValidation("X-Teacher-Secret", sharedSecret);
                using var resp = await _httpClient.SendAsync(req);
                if (resp.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    await Task.Delay(125);
                    continue;
                }

                resp.EnsureSuccessStatusCode();
                var answer = await resp.Content.ReadFromJsonAsync<DemoSessionStartResponse>();
                if (answer is null || string.IsNullOrWhiteSpace(answer.Sdp))
                {
                    _diagnosticLog.LogWarning($"Teacher demo answer payload was empty for {studentBaseUrl}.");
                    break;
                }

                var sdp = SDP.ParseSDPDescription(answer.Sdp);
                var answerType = Enum.TryParse<SIPSorcery.SIP.App.SdpType>(answer.SdpType, ignoreCase: true, out var parsedAnswerType)
                    ? parsedAnswerType
                    : SIPSorcery.SIP.App.SdpType.answer;
                var setRes = pc.SetRemoteDescription(answerType, sdp);
                if (setRes != SetDescriptionResultEnum.OK)
                {
                    _diagnosticLog.LogWarning($"Teacher demo failed to set remote description for {studentBaseUrl}: {setRes}.");
                    pc.Close($"set remote failed {setRes}");
                }
                else
                {
                    _diagnosticLog.LogInfo($"Teacher demo remote answer applied for {studentBaseUrl}: type={answerType}.");
                }

                break;
            }

            lock (_sync)
            {
                _students[studentBaseUrl] = new StudentConnection(pc, encodedHandler);
            }

            _ = Task.Run(async () =>
            {
                // Trickle ICE is only needed until the peer connects. Stop polling then
                // (or after ~30 s) so 8 students do not keep hammering their agents with
                // HTTP requests for the whole demonstration.
                for (var iteration = 0; iteration < 120 && IsCurrentConnection(studentBaseUrl, capturedPc); iteration++)
                {
                    if (capturedPc.connectionState == RTCPeerConnectionState.connected)
                    {
                        _diagnosticLog.LogInfo($"Teacher demo ICE polling finished for {studentBaseUrl}: peer connected.");
                        break;
                    }

                    try
                    {
                        using var req = new HttpRequestMessage(HttpMethod.Get, $"{studentBaseUrl}/api/demo/webrtc/ice/student?sessionId={Uri.EscapeDataString(sessionId)}");
                        req.Headers.TryAddWithoutValidation("X-Teacher-Secret", sharedSecret);
                        using var resp = await _httpClient.SendAsync(req);
                        resp.EnsureSuccessStatusCode();
                        var candidates = await resp.Content.ReadFromJsonAsync<List<WebRtcIceCandidateDto>>();
                        if (candidates is not null)
                        {
                            foreach (var c in candidates)
                            {
                                if (c.SdpMLineIndex is null)
                                {
                                    continue;
                                }

                                var count = Interlocked.Increment(ref remoteIceCandidates);
                                if (count == 1 || count % 10 == 0)
                                {
                                    _diagnosticLog.LogInfo($"Teacher demo remote ICE candidates from {studentBaseUrl}: {count}.");
                                }

                                capturedPc.addIceCandidate(new RTCIceCandidateInit { candidate = c.Candidate, sdpMid = c.SdpMid, sdpMLineIndex = (ushort)c.SdpMLineIndex.Value });
                            }
                        }
                    }
                    catch
                    {
                    }

                    await Task.Delay(250);
                }
            });

            _diagnosticLog.LogInfo($"Teacher demo start sequence completed for {studentBaseUrl}.");
        }
        catch (Exception ex)
        {
            _diagnosticLog.LogError($"Teacher demo start failed for {studentBaseUrl}: {ex}");
            if (encodedHandler is not null)
            {
                source.OnVideoSourceEncodedSample -= encodedHandler;
            }

            if (pc is not null)
            {
                try
                {
                    pc.Close("start failed");
                }
                catch
                {
                }
            }

            throw;
        }
    }

    public async Task StopAsync(string studentBaseUrl, string sharedSecret, string sessionId)
    {
        _diagnosticLog.LogInfo($"Teacher demo stop requested: student={studentBaseUrl}, sessionId={sessionId}.");

        StudentConnection? student;
        lock (_sync)
        {
            _students.Remove(studentBaseUrl, out student);
        }

        if (student is not null)
        {
            if (_sharedSource is not null)
            {
                _sharedSource.OnVideoSourceEncodedSample -= student.EncodedHandler;
            }

            try
            {
                student.Pc.Close("teacher stop");
                _diagnosticLog.LogInfo($"Teacher demo peer closed for {studentBaseUrl}.");
            }
            catch
            {
            }
        }

        ReleaseSharedSourceIfUnused();

        var stopReq = new DemoSessionStopRequest(sessionId);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{studentBaseUrl}/api/demo/webrtc/stop") { Content = JsonContent.Create(stopReq) };
        req.Headers.TryAddWithoutValidation("X-Teacher-Secret", sharedSecret);
        using (var resp = await _httpClient.SendAsync(req))
        {
            resp.EnsureSuccessStatusCode();
            _diagnosticLog.LogInfo($"Teacher demo stop request sent to {studentBaseUrl}: HTTP {(int)resp.StatusCode}.");
        }

        // Best-effort verification.
        try
        {
            using var statusReq = new HttpRequestMessage(HttpMethod.Get, $"{studentBaseUrl}/api/demo/status");
            statusReq.Headers.TryAddWithoutValidation("X-Teacher-Secret", sharedSecret);
            using var statusResp = await _httpClient.SendAsync(statusReq);
            statusResp.EnsureSuccessStatusCode();
            var status = await statusResp.Content.ReadFromJsonAsync<DemoSessionStatusDto>();
            if (status is not null && status.Active)
            {
                throw new InvalidOperationException("Student demo session is still active after stop.");
            }

            _diagnosticLog.LogInfo($"Teacher demo stop verified for {studentBaseUrl}.");
        }
        catch
        {
            // Do not throw on verification failure; stop is still requested.
            _diagnosticLog.LogWarning($"Teacher demo stop verification could not confirm inactive state for {studentBaseUrl}.");
        }
    }

    public void Dispose()
    {
        List<StudentConnection> students;
        IVideoSource? source;
        lock (_sync)
        {
            students = [.. _students.Values];
            _students.Clear();
            source = _sharedSource;
            _sharedSource = null;
        }

        foreach (var student in students)
        {
            if (source is not null)
            {
                source.OnVideoSourceEncodedSample -= student.EncodedHandler;
            }

            try
            {
                student.Pc.Close("dispose");
            }
            catch
            {
            }
        }

        if (source is not null)
        {
            try
            {
                source.CloseVideo().GetAwaiter().GetResult();
            }
            catch
            {
            }

            (source as IDisposable)?.Dispose();
        }

        _httpClient.Dispose();
    }

    private IVideoSource GetOrCreateSharedSource(DemoCaptureTarget target, int captureFps, VideoCodecsEnum preferredCodec)
    {
        lock (_sync)
        {
            if (_sharedSource is not null)
            {
                return _sharedSource;
            }

            var encoderDescription = OperatingSystem.IsMacOS() ? "H264 via VideoToolbox" : "VP8 via libvpx";
            _diagnosticLog.LogInfo($"Teacher demo creating shared capture source: {encoderDescription}, one encoder for all students.");

            var source = _videoSourceFactory.CreateSource(target, captureFps, _diagnosticLog, "shared");
            source.RestrictFormats(format => format.Codec == preferredCodec);
            source.OnVideoSourceError += (message) => _diagnosticLog.LogError($"Teacher demo shared video source error: {message}");

            long rawFrames = 0;
            long encodedSamples = 0;
            source.OnVideoSourceRawSample += (_, width, height, _, pixelFormat) =>
            {
                var count = Interlocked.Increment(ref rawFrames);
                if (count == 1 || count % 300 == 0)
                {
                    _diagnosticLog.LogInfo($"Teacher demo shared raw frames: {count} ({width}x{height} {pixelFormat}).");
                }
            };
            source.OnVideoSourceEncodedSample += (_, sample) =>
            {
                var count = Interlocked.Increment(ref encodedSamples);
                if (count == 1 || count % 300 == 0)
                {
                    _diagnosticLog.LogInfo($"Teacher demo shared encoded samples: {count} (bytes={sample?.Length ?? 0}).");
                }
            };

            _sharedSource = source;
            return source;
        }
    }

    private void DetachStudent(string studentBaseUrl, RTCPeerConnection pc, string reason)
    {
        lock (_sync)
        {
            if (!_students.TryGetValue(studentBaseUrl, out var student) || !ReferenceEquals(student.Pc, pc))
            {
                return;
            }

            _students.Remove(studentBaseUrl);
            if (_sharedSource is not null)
            {
                _sharedSource.OnVideoSourceEncodedSample -= student.EncodedHandler;
            }
        }

        _diagnosticLog.LogInfo($"Teacher demo detached {studentBaseUrl}: {reason}.");
        ReleaseSharedSourceIfUnused();
    }

    private void ReleaseSharedSourceIfUnused()
    {
        IVideoSource? source;
        lock (_sync)
        {
            if (_students.Count > 0 || _pendingStarts > 0 || _sharedSource is null)
            {
                return;
            }

            source = _sharedSource;
            _sharedSource = null;
        }

        _diagnosticLog.LogInfo("Teacher demo closing shared capture source (no students left).");
        _ = Task.Run(async () =>
        {
            try
            {
                await source.CloseVideo();
            }
            catch
            {
            }

            (source as IDisposable)?.Dispose();
        });
    }

    private bool IsCurrentConnection(string studentBaseUrl, RTCPeerConnection pc)
    {
        lock (_sync)
        {
            return _students.TryGetValue(studentBaseUrl, out var student) && ReferenceEquals(student.Pc, pc);
        }
    }

    private static void TryForceKeyFrame(IVideoSource source)
    {
        switch (source)
        {
            case WindowsRawScreenVideoSource w:
                w.ForceKeyFrame();
                break;
            case WindowsRawWindowVideoSource w:
                w.ForceKeyFrame();
                break;
            case MacOsRawScreenVideoSource m:
                m.ForceKeyFrame();
                break;
            case MacOsRawWindowVideoSource m:
                m.ForceKeyFrame();
                break;
            case Vp8EncodedRawVideoSource v:
                v.ForceKeyFrame();
                break;
        }
    }

    private static string GetTeacherDiagnosticLogPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDirectory = string.IsNullOrWhiteSpace(localAppData)
            ? AppContext.BaseDirectory
            : Path.Combine(localAppData, "TeacherServer", "TeacherClient.Avalonia");
        return Path.Combine(baseDirectory, "logs", "demo-webrtc.log");
    }
}
