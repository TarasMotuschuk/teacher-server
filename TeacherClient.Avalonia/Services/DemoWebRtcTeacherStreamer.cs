using System.Drawing;
using System.Net.Http.Json;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using Teacher.Common.Contracts;

namespace TeacherClient.CrossPlatform.Services;

public sealed class DemoWebRtcTeacherStreamer : IDisposable
{
    private readonly HttpClient _httpClient = new();
    private readonly Dictionary<string, RTCPeerConnection> _pcs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IVideoSource> _videoSources = new(StringComparer.Ordinal);
    private readonly DemoDiagnosticLog _diagnosticLog = new(GetTeacherDiagnosticLogPath());
    private readonly DemoVideoSourceFactory _videoSourceFactory = new();

    public async Task StartAsync(
        string studentBaseUrl,
        string sharedSecret,
        string sessionId,
        int captureX = 0,
        int captureY = 0,
        int captureWidth = 1280,
        int captureHeight = 720,
        int captureFps = 15)
    {
        if (_pcs.ContainsKey(studentBaseUrl))
        {
            _diagnosticLog.LogInfo($"Teacher demo start skipped: connection already exists for {studentBaseUrl}.");
            return;
        }

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

        if (OperatingSystem.IsMacOS())
        {
            _diagnosticLog.LogInfo($"Teacher demo WebRTC: H.264 encode via VideoToolbox for {studentBaseUrl}.");
        }
        else
        {
            _diagnosticLog.LogInfo($"Teacher demo WebRTC: VP8 encode via libvpx for {studentBaseUrl}.");
        }

        RTCPeerConnection? pc = null;
        long localIceCandidates = 0;
        long remoteIceCandidates = 0;
        var rect = new Rectangle(captureX, captureY, Math.Max(16, captureWidth), Math.Max(16, captureHeight));
        var source = _videoSourceFactory.CreateSource(rect, captureFps, _diagnosticLog, studentBaseUrl);

        try
        {
            if (OperatingSystem.IsMacOS())
            {
                source.RestrictFormats(format => format.Codec == VideoCodecsEnum.H264);
            }
            else
            {
                source.RestrictFormats(format => format.Codec == VideoCodecsEnum.VP8);
            }

            pc = new RTCPeerConnection(new RTCConfiguration { X_UseRtpFeedbackProfile = true });

            var videoTrack = new MediaStreamTrack(source.GetVideoSourceFormats(), MediaStreamStatusEnum.SendOnly);
            pc.addTrack(videoTrack);
            source.OnVideoSourceEncodedSample += pc.SendVideo;
            source.OnVideoSourceError += (message) =>
            {
                _diagnosticLog.LogError($"Teacher demo video source error for {studentBaseUrl}: {message}");
            };

            long rawFrames = 0;
            long encodedSamples = 0;
            source.OnVideoSourceRawSample += (_, width, height, _, pixelFormat) =>
            {
                var count = Interlocked.Increment(ref rawFrames);
                if (count == 1 || count % 60 == 0)
                {
                    _diagnosticLog.LogInfo($"Teacher demo raw frames for {studentBaseUrl}: {count} ({width}x{height} {pixelFormat}).");
                }
            };
            source.OnVideoSourceEncodedSample += (_, sample) =>
            {
                var count = Interlocked.Increment(ref encodedSamples);
                if (count == 1 || count % 60 == 0)
                {
                    _diagnosticLog.LogInfo($"Teacher demo encoded samples for {studentBaseUrl}: {count} (bytes={sample?.Length ?? 0}).");
                }
            };
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
                    _diagnosticLog.LogInfo($"Teacher demo starting video source for {studentBaseUrl}.");
                    await source.StartVideo();
                    _diagnosticLog.LogInfo($"Teacher demo video source start returned for {studentBaseUrl}.");
                }
                else if (state == RTCPeerConnectionState.closed || state == RTCPeerConnectionState.failed)
                {
                    _diagnosticLog.LogInfo($"Teacher demo closing video source for {studentBaseUrl} due to peer state {state}.");
                    await source.CloseVideo();
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

            _pcs[studentBaseUrl] = pc;
            _videoSources[studentBaseUrl] = source;

            _ = Task.Run(async () =>
            {
                while (_pcs.TryGetValue(studentBaseUrl, out var current) && ReferenceEquals(current, pc))
                {
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

                                pc.addIceCandidate(new RTCIceCandidateInit { candidate = c.Candidate, sdpMid = c.SdpMid, sdpMLineIndex = (ushort)c.SdpMLineIndex.Value });
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

            try
            {
                await source.CloseVideo();
            }
            catch
            {
            }

            (source as IDisposable)?.Dispose();
            throw;
        }
    }

    public async Task StopAsync(string studentBaseUrl, string sharedSecret, string sessionId)
    {
        _diagnosticLog.LogInfo($"Teacher demo stop requested: student={studentBaseUrl}, sessionId={sessionId}.");
        if (_pcs.Remove(studentBaseUrl, out var pc))
        {
            try
            {
                pc.Close("teacher stop");
                _diagnosticLog.LogInfo($"Teacher demo peer closed for {studentBaseUrl}.");
            }
            catch
            {
            }
        }

        if (_videoSources.Remove(studentBaseUrl, out var video))
        {
            try
            {
                await video.CloseVideo();
                (video as IDisposable)?.Dispose();
                _diagnosticLog.LogInfo($"Teacher demo video source disposed for {studentBaseUrl}.");
            }
            catch
            {
            }
        }

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
        foreach (var pc in _pcs.Values)
        {
            try
            {
                pc.Close("dispose");
            }
            catch
            {
            }
        }

        foreach (var v in _videoSources.Values)
        {
            try
            {
                (v as IDisposable)?.Dispose();
            }
            catch
            {
            }
        }

        _pcs.Clear();
        _videoSources.Clear();
        _httpClient.Dispose();
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
