using System.Drawing;

using SIPSorceryMedia.Abstractions;

namespace TeacherClient.CrossPlatform.Services;

public sealed class MacOsRawScreenVideoSource : IVideoSource, IDisposable
{
    private readonly Vp8EncodedRawVideoSource _inner;
    private readonly MacOsScreenCaptureProducer _producer;
    private readonly Rectangle _captureArea;
    private readonly int _captureFps;
    private bool _started;

    public MacOsRawScreenVideoSource(Rectangle captureArea, int captureFps)
    {
        _inner = new Vp8EncodedRawVideoSource(new VideoToolboxH264VideoEncoder(captureFps));
        _producer = new MacOsScreenCaptureProducer();
        _captureArea = captureArea;
        _captureFps = captureFps;
    }

    public event EncodedSampleDelegate? OnVideoSourceEncodedSample
    {
        add => _inner.OnVideoSourceEncodedSample += value;
        remove => _inner.OnVideoSourceEncodedSample -= value;
    }

    public event RawVideoSampleDelegate? OnVideoSourceRawSample
    {
        add => _inner.OnVideoSourceRawSample += value;
        remove => _inner.OnVideoSourceRawSample -= value;
    }

    public event RawVideoSampleFasterDelegate? OnVideoSourceRawSampleFaster
    {
        add => _inner.OnVideoSourceRawSampleFaster += value;
        remove => _inner.OnVideoSourceRawSampleFaster -= value;
    }

    public event SourceErrorDelegate? OnVideoSourceError
    {
        add => _inner.OnVideoSourceError += value;
        remove => _inner.OnVideoSourceError -= value;
    }

    public Task PauseVideo() => _inner.PauseVideo();

    public Task ResumeVideo() => _inner.ResumeVideo();

    public async Task StartVideo()
    {
        MacOsScreenCaptureProducer.EnsureScreenCaptureAccess();
        await _inner.StartVideo();

        if (!_started)
        {
            _started = true;
            _producer.Start(_captureArea, _captureFps, _inner.ExternalVideoSourceRawSample);
        }
    }

    public async Task CloseVideo()
    {
        await _inner.CloseVideo();
        await _producer.StopAsync();
        _started = false;
    }

    public List<VideoFormat> GetVideoSourceFormats() => _inner.GetVideoSourceFormats();

    public void SetVideoSourceFormat(VideoFormat videoFormat) => _inner.SetVideoSourceFormat(videoFormat);

    public void RestrictFormats(Func<VideoFormat, bool> filter) => _inner.RestrictFormats(filter);

    public void ForceKeyFrame() => _inner.ForceKeyFrame();

    public bool HasEncodedVideoSubscribers() => _inner.HasEncodedVideoSubscribers();

    public bool IsVideoSourcePaused() => _inner.IsVideoSourcePaused();

    public void ExternalVideoSourceRawSample(uint durationMilliseconds, int width, int height, byte[] sample, VideoPixelFormatsEnum pixelFormat)
        => _inner.ExternalVideoSourceRawSample(durationMilliseconds, width, height, sample, pixelFormat);

    public void ExternalVideoSourceRawSampleFaster(uint durationMilliseconds, RawImage rawImage)
        => _inner.ExternalVideoSourceRawSampleFaster(durationMilliseconds, rawImage);

    public void Dispose()
    {
        _started = false;
        _producer.Dispose();
        _inner.Dispose();
    }
}
