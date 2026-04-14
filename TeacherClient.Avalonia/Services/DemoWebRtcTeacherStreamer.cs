using System.Drawing;
using System.Net.Http.Json;
using FFmpeg.AutoGen;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.FFmpeg;
using Teacher.Common.Contracts;

namespace TeacherClient.CrossPlatform.Services;

public sealed class DemoWebRtcTeacherStreamer : IDisposable
{
    private readonly HttpClient _httpClient = new();
    private readonly Dictionary<string, RTCPeerConnection> _pcs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IVideoSource> _videoSources = new(StringComparer.Ordinal);

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
            return;
        }

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
            throw new InvalidOperationException($"Cannot reach student agent at {studentBaseUrl}. {ex.Message}", ex);
        }

        FfmpegBootstrap.TryConfigureBundledLibraries();
        var bundledLibDir = FfmpegBootstrap.TryGetBundledFfmpegLibDirectory();
        try
        {
            // SIPSorcery RegisterFFmpegBinaries only checks PATH or FFmpeg/bin/x64 unless libPath is set; it ignores ffmpeg.RootPath.
            FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_ERROR, bundledLibDir, null);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(BuildFfmpegErrorMessage(ex, bundledLibDir), ex);
        }

        var pc = new RTCPeerConnection(new RTCConfiguration { X_UseRtpFeedbackProfile = true });
        _pcs[studentBaseUrl] = pc;

        IVideoSource source;
        try
        {
            var rect = new Rectangle(captureX, captureY, Math.Max(16, captureWidth), Math.Max(16, captureHeight));
            source = new FFmpegScreenSource(GetScreenInputPath(), rect, captureFps);
        }
        catch
        {
            // Fallback: keep demonstration functional even if screen capture isn't available on this machine yet.
            var testPattern = new VideoTestPatternSource(new FFmpegVideoEncoder());
            testPattern.SetFrameRate(captureFps);
            source = testPattern;
        }

        source.RestrictFormats(format => format.Codec == VideoCodecsEnum.VP8 || format.Codec == VideoCodecsEnum.H264);
        _videoSources[studentBaseUrl] = source;

        var videoTrack = new MediaStreamTrack(source.GetVideoSourceFormats(), MediaStreamStatusEnum.SendOnly);
        pc.addTrack(videoTrack);
        source.OnVideoSourceEncodedSample += pc.SendVideo;
        pc.OnVideoFormatsNegotiated += (formats) => source.SetVideoSourceFormat(formats.First());

        pc.onicecandidate += (cand) =>
        {
            if (cand is null)
            {
                return;
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
            if (state == RTCPeerConnectionState.connected)
            {
                await source.StartVideo();
            }
            else if (state == RTCPeerConnectionState.closed || state == RTCPeerConnectionState.failed)
            {
                await source.CloseVideo();
            }
        };

        var offerInit = pc.createOffer();
        await pc.setLocalDescription(offerInit);

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
                break;
            }

            var sdp = SDP.ParseSDPDescription(answer.Sdp);
            var answerType = Enum.TryParse<SIPSorcery.SIP.App.SdpType>(answer.SdpType, ignoreCase: true, out var parsedAnswerType)
                ? parsedAnswerType
                : SIPSorcery.SIP.App.SdpType.answer;
            var setRes = pc.SetRemoteDescription(answerType, sdp);
            if (setRes != SetDescriptionResultEnum.OK)
            {
                pc.Close($"set remote failed {setRes}");
            }
            break;
        }

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
    }

    public async Task StopAsync(string studentBaseUrl, string sharedSecret, string sessionId)
    {
        if (_pcs.Remove(studentBaseUrl, out var pc))
        {
            try
            {
                pc.Close("teacher stop");
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
        }
        catch
        {
            // Do not throw on verification failure; stop is still requested.
        }
    }

    private static string GetScreenInputPath()
    {
        if (OperatingSystem.IsWindows())
        {
            return "desktop";
        }

        if (OperatingSystem.IsMacOS())
        {
            // avfoundation: the screen capture device is typically index 1 ("Capture screen 0").
            return "1";
        }

        if (OperatingSystem.IsLinux())
        {
            return ":0.0";
        }

        return "desktop";
    }

    private static string BuildFfmpegErrorMessage(Exception ex, string? bundledLibDirPassedToInit)
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "ffmpeg"),
            Path.Combine(baseDir, "ffmpeg", "bin"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "Frameworks", "ffmpeg")),
        };

        var root = string.IsNullOrWhiteSpace(ffmpeg.RootPath) ? "<empty>" : ffmpeg.RootPath;
        var lines = new List<string>
        {
            $"FFmpeg initialization failed: {ex.Message}",
            $"Bundled lib dir passed to FFmpegInit.Initialise = {bundledLibDirPassedToInit ?? "<null> (searches PATH / FFmpeg/bin/x64 only)"}",
            $"FFmpeg.AutoGen.ffmpeg.RootPath = {root}",
            $"BaseDirectory = {baseDir}",
            "Checked candidates:",
        };

        foreach (var c in candidates)
        {
            try
            {
                var exists = Directory.Exists(c);
                var avcodec = exists
                    ? Directory.EnumerateFiles(c, OperatingSystem.IsWindows() ? "avcodec*.dll" : "libavcodec*.dylib").Any()
                    : false;
                var avutil = exists
                    ? Directory.EnumerateFiles(c, OperatingSystem.IsWindows() ? "avutil*.dll" : "libavutil*.dylib").Any()
                    : false;
                lines.Add($"- {c} (exists={exists}, avcodec={avcodec}, avutil={avutil})");
            }
            catch
            {
                lines.Add($"- {c} (unreadable)");
            }
        }

        return string.Join(Environment.NewLine, lines);
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
}



