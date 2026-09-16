# Demo State And Tasks

## Current State

### Control plane

- Demo lifecycle is still controlled through the student service HTTP endpoints in [StudentAgent.Shared/Hosting/StudentAgentHostExtensions.cs](/Users/taras/Projects/OWN-GITHUB/teacher-server/StudentAgent.Shared/Hosting/StudentAgentHostExtensions.cs).
- The active session lives in-memory in [StudentAgent.Shared/Services/DemoSessionStore.cs](/Users/taras/Projects/OWN-GITHUB/teacher-server/StudentAgent.Shared/Services/DemoSessionStore.cs).
- `UIHost` polls `/api/demo/status` every 750 ms and starts or stops fullscreen demo locally in [StudentAgent.UIHost/UIHostApplicationContext.cs](/Users/taras/Projects/OWN-GITHUB/teacher-server/StudentAgent.UIHost/UIHostApplicationContext.cs).
- The previous offer-loss bug is fixed: offer SDP is no longer consumed before the student posts an answer.
- The previous `204 NoContent` JSON parsing bug is fixed: student retry logic now treats missing offer as a normal transient state.

### Teacher-side media path

- The Avalonia teacher client uses [TeacherClient.Avalonia/Services/DemoWebRtcTeacherStreamer.cs](/Users/taras/Projects/OWN-GITHUB/teacher-server/TeacherClient.Avalonia/Services/DemoWebRtcTeacherStreamer.cs) as the WebRTC sender.
- Video source creation is centralized in [TeacherClient.Avalonia/Services/DemoVideoSourceFactory.cs](/Users/taras/Projects/OWN-GITHUB/teacher-server/TeacherClient.Avalonia/Services/DemoVideoSourceFactory.cs).
- The current platform split is:
  - macOS teacher: raw screen capture -> VideoToolbox H.264 encoder -> WebRTC H.264
  - Windows teacher: raw screen capture -> libvpx VP8 encoder -> WebRTC VP8
  - fallback: test pattern source using VP8/libvpx

### macOS teacher pipeline

- Raw capture exists in [TeacherClient.Avalonia/Services/MacOsScreenCaptureProducer.cs](/Users/taras/Projects/OWN-GITHUB/teacher-server/TeacherClient.Avalonia/Services/MacOsScreenCaptureProducer.cs).
- It currently uses `CGDisplayCreateImageForRect` polling, not `ScreenCaptureKit`.
- The capture output is BGRA frames.
- Those frames flow through [TeacherClient.Avalonia/Services/MacOsRawScreenVideoSource.cs](/Users/taras/Projects/OWN-GITHUB/teacher-server/TeacherClient.Avalonia/Services/MacOsRawScreenVideoSource.cs).
- H.264 encoding is implemented in [TeacherClient.Avalonia/Services/VideoToolboxH264VideoEncoder.cs](/Users/taras/Projects/OWN-GITHUB/teacher-server/TeacherClient.Avalonia/Services/VideoToolboxH264VideoEncoder.cs).
- The VideoToolbox encoder produces Annex B H.264 chunks suitable for SIPSorcery packetization.

### Windows teacher pipeline

- Raw capture exists in [TeacherClient.Avalonia/Services/WindowsScreenCaptureProducer.cs](/Users/taras/Projects/OWN-GITHUB/teacher-server/TeacherClient.Avalonia/Services/WindowsScreenCaptureProducer.cs).
- It currently uses `Graphics.CopyFromScreen` polling.
- The capture output is BGRA frames.
- Those frames flow through [TeacherClient.Avalonia/Services/WindowsRawScreenVideoSource.cs](/Users/taras/Projects/OWN-GITHUB/teacher-server/TeacherClient.Avalonia/Services/WindowsRawScreenVideoSource.cs).
- Encoding is handled by [TeacherClient.Avalonia/Services/Vp8EncodedRawVideoSource.cs](/Users/taras/Projects/OWN-GITHUB/teacher-server/TeacherClient.Avalonia/Services/Vp8EncodedRawVideoSource.cs) via `SIPSorceryMedia.Encoders.VpxVideoEncoder`.

### Student-side receive path

- Student UI no longer uses the old FFmpeg video sink path.
- The receive path is implemented in [StudentAgent.UIHost/Services/DemoWebRtcVideoReceiveEndPoint.cs](/Users/taras/Projects/OWN-GITHUB/teacher-server/StudentAgent.UIHost/Services/DemoWebRtcVideoReceiveEndPoint.cs).
- Current decoder split is:
  - VP8: libvpx decode via `SIPSorceryMedia.Encoders.VpxVideoEncoder`
  - H.264: Media Foundation H.264 decoder MFT on Windows
- Decoded frames are rendered into the fullscreen forms in [StudentAgent.UIHost/UIHostApplicationContext.cs](/Users/taras/Projects/OWN-GITHUB/teacher-server/StudentAgent.UIHost/UIHostApplicationContext.cs).

### Logging and diagnostics

- Teacher and student both write `demo-webrtc.log`.
- Teacher logging includes start/stop lifecycle, selected codec path, ICE counts, raw frame counts, encoded sample counts, and peer state.
- Student logging includes status polling, offer/answer flow, peer state, RTP counts, decoder activity, and rendered frame counts.

## What Is Good Now

- The demo stack is no longer architecturally blocked by FFmpeg screen-device capture.
- macOS teacher already has a native capture path and a native H.264 encoder path.
- Windows teacher already has a raw capture path and a non-FFmpeg VP8 encoder path.
- Student receive logic supports both VP8 and H.264.
- The biggest signaling bugs that caused stuck demo sessions were addressed.

## Known Risks

- There is still no confirmed end-to-end validation for:
  - macOS teacher -> Windows student using H.264
  - Windows teacher -> Windows student using VP8
  - repeated start/stop cycles after errors
- macOS capture currently uses CoreGraphics snapshot polling. This is simple but not ideal for performance or OS integration.
- The architecture is still per-student peer connection. It is not yet the intended Veyon-style shared broadcast host.
- The current implementation still depends on software polling capture on both platforms.
- The teacher currently keeps one `_demoSessionId` in [TeacherClient.Avalonia/MainWindow.axaml.cs](/Users/taras/Projects/OWN-GITHUB/teacher-server/TeacherClient.Avalonia/MainWindow.axaml.cs) rather than a dedicated broadcast session object with participant state.

## Immediate Tasks

1. Run a real end-to-end test for macOS teacher -> Windows student and inspect both `demo-webrtc.log` files.
2. Verify that H.264 negotiation succeeds and that the student receives decoded frames, not only RTP packets.
3. Verify that Windows teacher -> Windows student still works through the VP8 path after the new raw-source architecture.
4. Verify repeated start/stop cycles and confirm that fullscreen demo does not remain stuck on the student.
5. Add explicit teacher-side diagnostics for selected negotiated codec and first successful keyframe delivery.

## Next Tasks

1. Replace macOS `CGDisplayCreateImageForRect` polling with a better capture backend, preferably `ScreenCaptureKit`.
2. Replace Windows `Graphics.CopyFromScreen` polling with a better capture backend such as `Windows.Graphics.Capture` or Desktop Duplication.
3. Introduce a real demo broadcast session object instead of one shared `_demoSessionId` string in the main window.
4. Move toward a Veyon-style broadcast architecture:
   - one capture pipeline per demo session
   - one encoder pipeline per demo session
   - fan-out to many peers without duplicating capture work
5. Add participant-level telemetry:
   - connected
   - negotiating
   - decoding
   - stalled
   - failed

## Decision Summary

- Keep WebRTC as the transport.
- Keep `StudentAgent.Service` as control plane and `StudentAgent.UIHost` as visible renderer/fullscreen host.
- Continue moving away from FFmpeg-based screen-device capture.
- Prefer native platform capture plus platform-native or lightweight encoder paths.
- Preserve the current logs-first debugging approach until one-PC demo is reliably validated end-to-end.
