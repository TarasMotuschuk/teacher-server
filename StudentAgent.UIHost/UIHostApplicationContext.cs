using System.Drawing.Imaging;
using System.Net.Http.Json;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.FFmpeg;
using StudentAgent.Services;
using StudentAgent.UI;
using StudentAgent.UI.Localization;
using Teacher.Common.Contracts;
using TeacherClient.CrossPlatform.Services;

namespace StudentAgent.UIHost;

public sealed class UIHostApplicationContext : AgentUiApplicationContextBase
{
    private readonly AgentLogService _logService;
    private readonly DemoDiagnosticLog _demoDiagnosticLog;
    private readonly System.Windows.Forms.Timer _desktopIconRestoreTimer;
    private readonly System.Windows.Forms.Timer _demoPollTimer;
    private readonly HttpClient _httpClient = new();
    private readonly List<DemoFullscreenForm> _demoForms = [];
    private string? _activeDemoSessionId;
    private RTCPeerConnection? _demoPc;
    private FFmpegVideoEndPoint? _demoVideoEndPoint;
    private bool _demoAuthWarningShown;
    private DateTime _lastDemoConnectivityWarningUtc;
    private DateTime _lastDemoWebRtcInitAttemptUtc;

    public UIHostApplicationContext(AgentSettingsStore settingsStore, AgentLogService logService, ProcessService processService)
        : base(settingsStore, logService, processService)
    {
        _logService = logService;
        _demoDiagnosticLog = new DemoDiagnosticLog(Path.Combine(StudentAgentPathHelper.GetLogsDirectory(), "demo-webrtc.log"));
        _desktopIconRestoreTimer = new System.Windows.Forms.Timer();
        _desktopIconRestoreTimer.Tick += (_, _) => RestoreDesktopIconsSilently();
        ConfigureDesktopIconRestoreTimer();
        _desktopIconRestoreTimer.Start();
        settingsStore.SettingsChanged += SettingsStore_OnSettingsChanged;

        _demoPollTimer = new System.Windows.Forms.Timer { Interval = 750 };
        _demoPollTimer.Tick += async (_, _) => await PollDemoAsync();
        _demoPollTimer.Start();
    }

    protected override void HandleExitRequested()
    {
        if (!PromptForPassword())
        {
            return;
        }

        ExitThread();
    }

    protected override void OnBeforeExitThreadCore()
    {
        SettingsStore.SettingsChanged -= SettingsStore_OnSettingsChanged;
        _desktopIconRestoreTimer.Stop();
        _desktopIconRestoreTimer.Dispose();
        _demoPollTimer.Stop();
        _demoPollTimer.Dispose();
        CloseDemoForms();
        _logService.LogInfo("StudentAgent.UIHost stopping.");
    }

    private void SettingsStore_OnSettingsChanged(object? sender, EventArgs e)
    {
        ConfigureDesktopIconRestoreTimer();
    }

    private void ConfigureDesktopIconRestoreTimer()
    {
        var minutes = Math.Max(1, SettingsStore.Current.DesktopIconAutoRestoreMinutes);
        _desktopIconRestoreTimer.Interval = checked((int)TimeSpan.FromMinutes(minutes).TotalMilliseconds);
    }

    private async Task PollDemoAsync()
    {
        try
        {
            // UIHost is session-scoped; the service exposes the API on localhost.
            var port = Math.Max(1, SettingsStore.Current.Port);
            using var req = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/api/demo/status");
            req.Headers.TryAddWithoutValidation("X-Teacher-Secret", SettingsStore.CurrentCached.SharedSecret);
            using var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                if ((int)resp.StatusCode == 401 || (int)resp.StatusCode == 403)
                {
                    if (!_demoAuthWarningShown)
                    {
                        _demoAuthWarningShown = true;
                        LogService.LogWarning("Demonstration: UIHost cannot read /api/demo/status due to unauthorized teacher secret (401/403).");
                        _demoDiagnosticLog.LogWarning("Student demo status polling failed with unauthorized teacher secret (401/403).");
                        ShowTrayNotification(
                            StudentAgentText.AgentName,
                            "Demonstration is blocked: teacher secret mismatch. Open Settings and check Shared Secret.");
                    }
                }
                else
                {
                    var now = DateTime.UtcNow;
                    if (now - _lastDemoConnectivityWarningUtc > TimeSpan.FromMinutes(2))
                    {
                        _lastDemoConnectivityWarningUtc = now;
                        LogService.LogWarning($"Demonstration: UIHost cannot read /api/demo/status. HTTP {(int)resp.StatusCode}.");
                        _demoDiagnosticLog.LogWarning($"Student demo status polling failed. HTTP {(int)resp.StatusCode}.");
                    }
                }

                return;
            }

            var status = await resp.Content.ReadFromJsonAsync<DemoSessionStatusDto>();
            if (status is null || !status.Active || string.IsNullOrWhiteSpace(status.SessionId) || !status.FullscreenLock)
            {
                if (_activeDemoSessionId is not null)
                {
                    _demoDiagnosticLog.LogInfo($"Student demo session no longer active. Closing viewer for sessionId={_activeDemoSessionId}.");
                    _activeDemoSessionId = null;
                    CloseDemoForms();
                }

                return;
            }

            if (!string.Equals(_activeDemoSessionId, status.SessionId, StringComparison.Ordinal))
            {
                _demoAuthWarningShown = false;
                _activeDemoSessionId = status.SessionId;
                _demoDiagnosticLog.LogInfo($"Student demo session activated: sessionId={status.SessionId}, fullscreenLock={status.FullscreenLock}, startedUtc={status.StartedUtc:O}.");
                ShowDemoForms();
                _lastDemoWebRtcInitAttemptUtc = DateTime.MinValue;
                await StartOrRefreshDemoWebRtcAsync(status.SessionId);
            }
            else
            {
                // If the demo session is active but WebRTC was never established (or was torn down),
                // retry periodically so transient FFmpeg/init errors don't leave students stuck on a black screen.
                if (_demoPc is null && DateTime.UtcNow - _lastDemoWebRtcInitAttemptUtc > TimeSpan.FromSeconds(5))
                {
                    _lastDemoWebRtcInitAttemptUtc = DateTime.UtcNow;
                    _demoDiagnosticLog.LogWarning($"Student demo peer missing while session {status.SessionId} is active. Retrying WebRTC init.");
                    await StartOrRefreshDemoWebRtcAsync(status.SessionId);
                }
            }
        }
        catch
        {
            // Silent by design: demo mode is optional and should not crash the tray host.
        }
    }

    private async Task StartOrRefreshDemoWebRtcAsync(string sessionId)
    {
        try
        {
            _lastDemoWebRtcInitAttemptUtc = DateTime.UtcNow;
            _logService.LogInfo($"Demo WebRTC: starting/refreshing for sessionId={sessionId}.");
            _demoDiagnosticLog.LogInfo($"Student demo WebRTC init starting: sessionId={sessionId}.");
            var port = Math.Max(1, SettingsStore.Current.Port);
            using var offerReq = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/api/demo/webrtc/offer?sessionId={Uri.EscapeDataString(sessionId)}");
            offerReq.Headers.TryAddWithoutValidation("X-Teacher-Secret", SettingsStore.CurrentCached.SharedSecret);
            using var offerResp = await _httpClient.SendAsync(offerReq);
            if (!offerResp.IsSuccessStatusCode)
            {
                _logService.LogWarning($"Demo WebRTC: offer request failed. HTTP {(int)offerResp.StatusCode}.");
                _demoDiagnosticLog.LogWarning($"Student demo offer request failed: HTTP {(int)offerResp.StatusCode}.");
                return;
            }

            var offer = await offerResp.Content.ReadFromJsonAsync<DemoSessionStartRequest>();
            if (offer is null || string.IsNullOrWhiteSpace(offer.Sdp))
            {
                _logService.LogWarning("Demo WebRTC: offer response is empty or invalid.");
                _demoDiagnosticLog.LogWarning("Student demo offer response is empty or invalid.");
                return;
            }

            // Initialise FFmpeg once per process using the bundled native libraries.
            var bundledLibDir = FfmpegBootstrap.EnsureInitialized();
            _logService.LogInfo($"Demo WebRTC: initialising FFmpeg (bundledLibDir={bundledLibDir ?? "<null>"}).");
            _demoDiagnosticLog.LogInfo($"Student demo FFmpeg initialised: bundledLibDir={bundledLibDir ?? "<null>"}.");

            _demoVideoEndPoint = new FFmpegVideoEndPoint();
            _demoVideoEndPoint.RestrictFormats(format => format.Codec == VideoCodecsEnum.VP8 || format.Codec == VideoCodecsEnum.H264);
            long decodedFrames = 0;
            _demoVideoEndPoint.OnVideoSinkDecodedSampleFaster += (RawImage rawImage) =>
            {
                var decodedCount = Interlocked.Increment(ref decodedFrames);
                if (decodedCount == 1 || decodedCount % 100 == 0)
                {
                    _demoDiagnosticLog.LogInfo(
                        $"Student demo decoded frames={decodedCount}, pixelFormat={rawImage.PixelFormat}, size={rawImage.Width}x{rawImage.Height}, stride={rawImage.Stride}.");
                }

                PixelFormat? pixelFormat = rawImage.PixelFormat switch
                {
                    // GDI+ "Format24bppRgb" is actually stored as BGR in memory; accept both.
                    VideoPixelFormatsEnum.Rgb => PixelFormat.Format24bppRgb,
                    VideoPixelFormatsEnum.Bgr => PixelFormat.Format24bppRgb,
                    VideoPixelFormatsEnum.Bgra => PixelFormat.Format32bppArgb,
                    _ => null,
                };

                if (pixelFormat is null)
                {
                    return;
                }

                foreach (var form in _demoForms)
                {
                    form.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            using var bmp = new Bitmap(rawImage.Width, rawImage.Height, rawImage.Stride, pixelFormat.Value, rawImage.Sample);
                            form.SetFrame((Bitmap)bmp.Clone());
                            if (decodedCount == 1 || decodedCount % 100 == 0)
                            {
                                _demoDiagnosticLog.LogInfo($"Student demo rendered frame #{decodedCount} to fullscreen form.");
                            }
                        }
                        catch
                        {
                        }
                    }));
                }
            };

            _demoPc = new RTCPeerConnection(new RTCConfiguration { X_UseRtpFeedbackProfile = true });
            var videoTrack = new MediaStreamTrack(_demoVideoEndPoint.GetVideoSinkFormats(), MediaStreamStatusEnum.RecvOnly);
            _demoPc.addTrack(videoTrack);
            long receivedVideoFrames = 0;
            _demoPc.OnVideoFrameReceived += (remoteEndPoint, timestamp, payload, format) =>
            {
                var receivedCount = Interlocked.Increment(ref receivedVideoFrames);
                if (receivedCount == 1 || receivedCount % 100 == 0)
                {
                    _demoDiagnosticLog.LogInfo($"Student demo received RTP video frames={receivedCount}, payloadBytes={payload?.Length ?? 0}, format={format.Codec}.");
                }

                _demoVideoEndPoint.GotVideoFrame(remoteEndPoint, timestamp, payload ?? Array.Empty<byte>(), format);
            };
            _demoPc.OnVideoFormatsNegotiated += (formats) =>
            {
                try
                {
                    var chosen = formats.First();
                    _logService.LogInfo($"Demo WebRTC: negotiated video format {chosen.Codec} {chosen.FormatID} {chosen.ClockRate}.");
                    _demoDiagnosticLog.LogInfo($"Student demo negotiated video format: codec={chosen.Codec}, formatId={chosen.FormatID}, clockRate={chosen.ClockRate}.");
                    _demoVideoEndPoint.SetVideoSinkFormat(chosen);
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"Demo WebRTC: failed to apply negotiated video formats: {ex.Message}");
                    _demoDiagnosticLog.LogWarning($"Student demo failed to apply negotiated video format: {ex.Message}");
                }
            };

            long localIceCandidates = 0;
            _demoPc.onicecandidate += (cand) =>
            {
                if (cand is null)
                {
                    return;
                }

                var count = Interlocked.Increment(ref localIceCandidates);
                if (count == 1 || count % 10 == 0)
                {
                    _demoDiagnosticLog.LogInfo($"Student demo local ICE candidates={count} for sessionId={sessionId}.");
                }

                var dto = new WebRtcIceCandidateDto(sessionId, cand.candidate, cand.sdpMid, cand.sdpMLineIndex);
                _ = _httpClient.PostAsJsonAsync($"http://127.0.0.1:{port}/api/demo/webrtc/ice/student", dto);
            };

            _demoPc.onconnectionstatechange += (state) =>
            {
                _demoDiagnosticLog.LogInfo($"Student demo peer state: {state} for sessionId={sessionId}.");
            };

            var sdpOffer = SDP.ParseSDPDescription(offer.Sdp);
            var offerType = Enum.TryParse<SIPSorcery.SIP.App.SdpType>(offer.SdpType, ignoreCase: true, out var parsedOfferType)
                ? parsedOfferType
                : SIPSorcery.SIP.App.SdpType.offer;
            _logService.LogInfo($"Demo WebRTC: setting remote description (type={offerType}).");
            _demoDiagnosticLog.LogInfo($"Student demo applying remote offer: type={offerType}.");
            var setRes = _demoPc.SetRemoteDescription(offerType, sdpOffer);
            if (setRes != SetDescriptionResultEnum.OK)
            {
                _logService.LogWarning($"Demo WebRTC: failed to set remote description: {setRes}.");
                _demoDiagnosticLog.LogWarning($"Student demo failed to set remote description: {setRes}.");
                return;
            }

            var answerInit = _demoPc.createAnswer();
            await _demoPc.setLocalDescription(answerInit);
            var answer = new DemoSessionStartResponse(_demoPc.localDescription.type.ToString(), _demoPc.localDescription.sdp.ToString());
            using (var ansReq = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}/api/demo/webrtc/answer?sessionId={Uri.EscapeDataString(sessionId)}")
            {
                Content = JsonContent.Create(answer),
            })
            {
                ansReq.Headers.TryAddWithoutValidation("X-Teacher-Secret", SettingsStore.CurrentCached.SharedSecret);
                using var ansResp = await _httpClient.SendAsync(ansReq);
                if (!ansResp.IsSuccessStatusCode)
                {
                    _logService.LogWarning($"Demo WebRTC: posting answer failed. HTTP {(int)ansResp.StatusCode}.");
                    _demoDiagnosticLog.LogWarning($"Student demo posting answer failed: HTTP {(int)ansResp.StatusCode}.");
                    return;
                }

                _logService.LogInfo("Demo WebRTC: posted answer.");
                _demoDiagnosticLog.LogInfo("Student demo answer posted.");
            }

            _ = Task.Run(async () =>
            {
                long remoteIceCandidates = 0;
                while (_activeDemoSessionId == sessionId)
                {
                    try
                    {
                        using var iceReq = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/api/demo/webrtc/ice/teacher?sessionId={Uri.EscapeDataString(sessionId)}");
                        iceReq.Headers.TryAddWithoutValidation("X-Teacher-Secret", SettingsStore.CurrentCached.SharedSecret);
                        using var iceResp = await _httpClient.SendAsync(iceReq);
                        if (!iceResp.IsSuccessStatusCode)
                        {
                            await Task.Delay(250);
                            continue;
                        }

                        var candidates = await iceResp.Content.ReadFromJsonAsync<List<WebRtcIceCandidateDto>>();
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
                                    _demoDiagnosticLog.LogInfo($"Student demo remote ICE candidates={count} for sessionId={sessionId}.");
                                }

                                _demoPc.addIceCandidate(new RTCIceCandidateInit { candidate = c.Candidate, sdpMid = c.SdpMid, sdpMLineIndex = (ushort)c.SdpMLineIndex.Value });
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
        catch (Exception ex)
        {
            _logService.LogWarning($"Demo WebRTC init failed: {ex.Message}");
            _demoDiagnosticLog.LogError($"Student demo WebRTC init failed for sessionId={sessionId}: {ex}");
        }
    }

    private void ShowDemoForms()
    {
        CloseDemoForms();
        foreach (var screen in Screen.AllScreens)
        {
            var form = new DemoFullscreenForm(screen);
            _demoForms.Add(form);
            form.Show();
        }

        _demoDiagnosticLog.LogInfo($"Student demo fullscreen forms shown: count={_demoForms.Count}.");
    }

    private void CloseDemoForms()
    {
        try
        {
            _demoPc?.Close("demo stopped");
            _demoPc = null;
            _demoDiagnosticLog.LogInfo("Student demo peer closed.");
        }
        catch
        {
        }

        try
        {
            _demoVideoEndPoint?.CloseVideo().GetAwaiter().GetResult();
            _demoVideoEndPoint = null;
            _demoDiagnosticLog.LogInfo("Student demo video endpoint closed.");
        }
        catch
        {
        }

        foreach (var form in _demoForms.ToArray())
        {
            try
            {
                form.ForceClose();
                form.Dispose();
            }
            catch
            {
            }
        }

        _demoForms.Clear();
        _demoDiagnosticLog.LogInfo("Student demo fullscreen forms cleared.");
    }

    private void RestoreDesktopIconsSilently()
    {
        try
        {
            var layoutPath = StudentAgentPathHelper.GetDesktopLayoutFilePath("default");
            if (!File.Exists(layoutPath))
            {
                return;
            }

            var args = new[]
            {
                "desktop-icons",
                "restore",
                "default",
                Path.Combine(StudentAgentPathHelper.GetDesktopLayoutResultsDirectory(), "timer-restore.json"),
            };

            _ = DesktopIcons.DesktopIconLayoutCommandRunner.TryExecute(args, _logService, out _, out var resultPath);

            if (!string.IsNullOrWhiteSpace(resultPath) && File.Exists(resultPath))
            {
                try
                {
                    File.Delete(resultPath);
                }
                catch
                {
                    // Ignore timer result cleanup failures.
                }
            }
        }
        catch
        {
            // Silent by design: this mirrors DesktopIconSaver's periodic restore behavior.
        }
    }
}
