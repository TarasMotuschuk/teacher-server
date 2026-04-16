using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Encoders;

namespace TeacherClient.CrossPlatform.Services;

/// <summary>
/// Accepts externally captured raw frames and encodes VP8 via libvpx (no FFmpeg).
/// </summary>
public sealed class Vp8EncodedRawVideoSource : IVideoSource, IDisposable
{
    private readonly IVideoEncoder _encoder;
    private readonly bool _ownsEncoder;
    private readonly object _sync = new();
    private readonly List<VideoFormat> _supportedFormats;
    private VideoFormat _selectedFormat;
    private bool _isPaused;
    private bool _isClosed;

    public Vp8EncodedRawVideoSource(uint? targetKbps = 2500)
        : this(CreateDefaultVpxEncoder(targetKbps), ownsEncoder: true)
    {
    }

    public Vp8EncodedRawVideoSource(IVideoEncoder encoder, bool ownsEncoder = true)
    {
        _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        _ownsEncoder = ownsEncoder;

        _supportedFormats = _encoder.SupportedFormats.ToList();
        if (_supportedFormats.Count == 0)
        {
            throw new NotSupportedException("Encoder did not report any SupportedFormats.");
        }

        _selectedFormat = _supportedFormats[0];
    }

    private static VpxVideoEncoder CreateDefaultVpxEncoder(uint? targetKbps)
    {
        var encoder = new VpxVideoEncoder();
        if (targetKbps is > 0)
        {
            encoder.TargetKbps = targetKbps;
        }

        return encoder;
    }

    public event EncodedSampleDelegate? OnVideoSourceEncodedSample;

    public event RawVideoSampleDelegate? OnVideoSourceRawSample;

    public event RawVideoSampleFasterDelegate? OnVideoSourceRawSampleFaster;

    public event SourceErrorDelegate? OnVideoSourceError;

    public Task PauseVideo()
    {
        lock (_sync)
        {
            _isPaused = true;
        }

        return Task.CompletedTask;
    }

    public Task ResumeVideo()
    {
        lock (_sync)
        {
            _isPaused = false;
        }

        return Task.CompletedTask;
    }

    public Task StartVideo()
    {
        lock (_sync)
        {
            _isClosed = false;
            _isPaused = false;
        }

        return Task.CompletedTask;
    }

    public Task CloseVideo()
    {
        lock (_sync)
        {
            _isClosed = true;
            _isPaused = false;
        }

        return Task.CompletedTask;
    }

    public List<VideoFormat> GetVideoSourceFormats() => [.. _supportedFormats];

    public void SetVideoSourceFormat(VideoFormat videoFormat)
    {
        if (_supportedFormats.All(format => format.Codec != videoFormat.Codec || format.FormatID != videoFormat.FormatID))
        {
            return;
        }

        _selectedFormat = videoFormat;
    }

    public void RestrictFormats(Func<VideoFormat, bool> filter)
    {
        var filtered = _supportedFormats.Where(filter).ToList();
        if (filtered.Count == 0)
        {
            return;
        }

        _supportedFormats.Clear();
        _supportedFormats.AddRange(filtered);
        if (_supportedFormats.All(format => format.Codec != _selectedFormat.Codec || format.FormatID != _selectedFormat.FormatID))
        {
            _selectedFormat = _supportedFormats[0];
        }
    }

    public void ExternalVideoSourceRawSample(uint durationMilliseconds, int width, int height, byte[] sample, VideoPixelFormatsEnum pixelFormat)
    {
        if (sample is null)
        {
            return;
        }

        OnVideoSourceRawSample?.Invoke(durationMilliseconds, width, height, sample, pixelFormat);
        EncodeAndRaise(durationMilliseconds, width, height, sample, pixelFormat);
    }

    public void ExternalVideoSourceRawSampleFaster(uint durationMilliseconds, RawImage rawImage)
    {
        OnVideoSourceRawSampleFaster?.Invoke(durationMilliseconds, rawImage);
        EncodeAndRaise(durationMilliseconds, rawImage.Width, rawImage.Height, rawImage.GetBuffer(), rawImage.PixelFormat);
    }

    public void ForceKeyFrame() => _encoder.ForceKeyFrame();

    public bool HasEncodedVideoSubscribers() => OnVideoSourceEncodedSample is not null;

    public bool IsVideoSourcePaused()
    {
        lock (_sync)
        {
            return _isPaused;
        }
    }

    public void Dispose()
    {
        _ = CloseVideo();
        if (_ownsEncoder && _encoder is IDisposable d)
        {
            d.Dispose();
        }
    }

    private void EncodeAndRaise(uint durationMilliseconds, int width, int height, byte[] sample, VideoPixelFormatsEnum pixelFormat)
    {
        lock (_sync)
        {
            if (_isClosed || _isPaused || OnVideoSourceEncodedSample is null)
            {
                return;
            }
        }

        try
        {
            var encoded = _encoder.EncodeVideo(width, height, sample, pixelFormat, _selectedFormat.Codec);
            if (encoded is null || encoded.Length == 0)
            {
                return;
            }

            var durationRtpUnits = Math.Max(1u, durationMilliseconds * 90);
            OnVideoSourceEncodedSample?.Invoke(durationRtpUnits, encoded);
        }
        catch (Exception ex)
        {
            OnVideoSourceError?.Invoke($"Video encode failed ({_selectedFormat.Codec}): {ex.Message}");
        }
    }
}
