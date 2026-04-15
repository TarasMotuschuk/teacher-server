using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.FFmpeg;

namespace TeacherClient.CrossPlatform.Services;

/// <summary>
/// An <see cref="IVideoSource"/> that accepts externally captured raw frames and encodes them with FFmpeg.
/// It deliberately does not perform any device capture on its own.
/// </summary>
public sealed class FfmpegEncodedRawVideoSource : IVideoSource, IDisposable
{
    private readonly FFmpegVideoEncoder _encoder;
    private readonly object _sync = new();
    private readonly List<VideoFormat> _supportedFormats;
    private VideoFormat _selectedFormat;
    private bool _isPaused;
    private bool _isClosed;

    public FfmpegEncodedRawVideoSource(VideoCodecsEnum preferredCodec = VideoCodecsEnum.H264)
    {
        FfmpegBootstrap.EnsureEncoderOnlyConfigured();
        _encoder = new FFmpegVideoEncoder();
        _supportedFormats = _encoder.SupportedFormats
            .Where(format => format.Codec == preferredCodec || (preferredCodec == VideoCodecsEnum.H264 && format.Codec == VideoCodecsEnum.VP8))
            .ToList();

        if (_supportedFormats.Count == 0)
        {
            throw new NotSupportedException($"No supported FFmpeg video formats found for preferred codec {preferredCodec}.");
        }

        _selectedFormat = _supportedFormats.FirstOrDefault(format => format.Codec == preferredCodec);
        if (_selectedFormat.Codec != preferredCodec)
        {
            _selectedFormat = _supportedFormats[0];
        }
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

    public void ForceKeyFrame()
    {
        _encoder.ForceKeyFrame();
    }

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

            // For video, the RTP clock is typically 90kHz.
            var durationRtpUnits = Math.Max(1u, durationMilliseconds * 90);
            OnVideoSourceEncodedSample?.Invoke(durationRtpUnits, encoded);
        }
        catch (Exception ex)
        {
            OnVideoSourceError?.Invoke($"Raw video encode failed: {ex.Message}");
        }
    }
}
