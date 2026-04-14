using System.Drawing.Imaging;
using System.Net.Http.Json;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.FFmpeg;
using StudentAgent.Services;
using TeacherClient.CrossPlatform.Services;
using StudentAgent.UI;
using StudentAgent.UI.Localization;
using Teacher.Common.Contracts;

namespace StudentAgent.UIHost;

public sealed class UIHostApplicationContext : AgentUiApplicationContextBase
{
    private readonly AgentLogService _logService;
    private readonly System.Windows.Forms.Timer _desktopIconRestoreTimer;
    private readonly System.Windows.Forms.Timer _demoPollTimer;
    private readonly HttpClient _httpClient = new();
    private readonly List<DemoFullscreenForm> _demoForms = [];
    private string? _activeDemoSessionId;
    private RTCPeerConnection? _demoPc;
    private FFmpegVideoEndPoint? _demoVideoEndPoint;
    private bool _demoAuthWarningShown;
    private DateTime _lastDemoConnectivityWarningUtc;

    public UIHostApplicationContext(AgentSettingsStore settingsStore, AgentLogService logService, ProcessService processService)
        : base(settingsStore, logService, processService)
    {
        _logService = logService;
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
                    }
                }

                return;
            }

            var status = await resp.Content.ReadFromJsonAsync<DemoSessionStatusDto>();
            if (status is null || !status.Active || string.IsNullOrWhiteSpace(status.SessionId) || !status.FullscreenLock)
            {
                if (_activeDemoSessionId is not null)
                {
                    _activeDemoSessionId = null;
                    CloseDemoForms();
                }

                return;
            }

            if (!string.Equals(_activeDemoSessionId, status.SessionId, StringComparison.Ordinal))
            {
                _demoAuthWarningShown = false;
                _activeDemoSessionId = status.SessionId;
                ShowDemoForms();
                await StartOrRefreshDemoWebRtcAsync(status.SessionId);
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
            var port = Math.Max(1, SettingsStore.Current.Port);
            using var offerReq = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/api/demo/webrtc/offer?sessionId={Uri.EscapeDataString(sessionId)}");
            offerReq.Headers.TryAddWithoutValidation("X-Teacher-Secret", SettingsStore.CurrentCached.SharedSecret);
            using var offerResp = await _httpClient.SendAsync(offerReq);
            if (!offerResp.IsSuccessStatusCode)
            {
                return;
            }

            var offer = await offerResp.Content.ReadFromJsonAsync<DemoSessionStartRequest>();
            if (offer is null || string.IsNullOrWhiteSpace(offer.Sdp))
            {
                return;
            }

            // Initialise FFmpeg once. Pass bundled lib directory — FFmpegInit ignores ffmpeg.RootPath unless libPath is set.
            FfmpegBootstrap.TryConfigureBundledLibraries();
            var bundledLibDir = FfmpegBootstrap.TryGetBundledFfmpegLibDirectory();
            FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_ERROR, bundledLibDir, null);

            _demoVideoEndPoint = new FFmpegVideoEndPoint();
            _demoVideoEndPoint.RestrictFormats(format => format.Codec == VideoCodecsEnum.VP8 || format.Codec == VideoCodecsEnum.H264);
            _demoVideoEndPoint.OnVideoSinkDecodedSampleFaster += (RawImage rawImage) =>
            {
                if (rawImage.PixelFormat != VideoPixelFormatsEnum.Rgb)
                {
                    return;
                }

                foreach (var form in _demoForms)
                {
                    form.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            using var bmp = new Bitmap(rawImage.Width, rawImage.Height, rawImage.Stride, PixelFormat.Format24bppRgb, rawImage.Sample);
                            form.SetFrame((Bitmap)bmp.Clone());
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
            _demoPc.OnVideoFrameReceived += _demoVideoEndPoint.GotVideoFrame;
            _demoPc.OnVideoFormatsNegotiated += (formats) => _demoVideoEndPoint.SetVideoSinkFormat(formats.First());

            _demoPc.onicecandidate += (cand) =>
            {
                if (cand is null)
                {
                    return;
                }

                var dto = new WebRtcIceCandidateDto(sessionId, cand.candidate, cand.sdpMid, cand.sdpMLineIndex);
                _ = _httpClient.PostAsJsonAsync($"http://127.0.0.1:{port}/api/demo/webrtc/ice/student", dto);
            };

            var sdpOffer = SDP.ParseSDPDescription(offer.Sdp);
            var offerType = Enum.TryParse<SIPSorcery.SIP.App.SdpType>(offer.SdpType, ignoreCase: true, out var parsedOfferType)
                ? parsedOfferType
                : SIPSorcery.SIP.App.SdpType.offer;
            var setRes = _demoPc.SetRemoteDescription(offerType, sdpOffer);
            if (setRes != SetDescriptionResultEnum.OK)
            {
                _logService.LogWarning($"Demo WebRTC: failed to set remote description: {setRes}.");
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
                await _httpClient.SendAsync(ansReq);
            }

            _ = Task.Run(async () =>
            {
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
    }

    private void CloseDemoForms()
    {
        try
        {
            _demoPc?.Close("demo stopped");
            _demoPc = null;
        }
        catch
        {
        }

        try
        {
            _demoVideoEndPoint?.CloseVideo().GetAwaiter().GetResult();
            _demoVideoEndPoint = null;
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
