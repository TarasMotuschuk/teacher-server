using System.Net;

using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Encoders;

namespace StudentAgent.UIHost.Services;

/// <summary>
/// Minimal VP8 receive endpoint that decodes VP8 frames using libvpx (no FFmpeg).
/// Designed specifically for the demo WebRTC viewer path.
/// </summary>
public sealed class VpxVp8VideoEndPoint : IDisposable
{
    private readonly VpxVideoEncoder _codec = new();
    private readonly List<VideoFormat> _formats = [new VideoFormat(VideoCodecsEnum.VP8, VpxVideoEncoder.VP8_FORMATID)];
    private VideoFormat _selected;
    private long _decodeAttempts;
    private long _decodeSuccess;
    private long _decodeFailures;
    private long _descriptorStrips;

    public VpxVp8VideoEndPoint()
    {
        _selected = _formats[0];
    }

    public event Action<uint, int, int, byte[], VideoPixelFormatsEnum>? OnDecodedFrame;
    public event Action<string>? OnDiagnostic;

    public List<VideoFormat> GetVideoSinkFormats() => [.. _formats];

    public void RestrictFormats(Func<VideoFormat, bool> filter)
    {
        var filtered = _formats.Where(filter).ToList();
        if (filtered.Count == 0)
        {
            return;
        }

        _formats.Clear();
        _formats.AddRange(filtered);
        if (_formats.All(f => f.Codec != _selected.Codec || f.FormatID != _selected.FormatID))
        {
            _selected = _formats[0];
        }
    }

    public void SetVideoSinkFormat(VideoFormat format)
    {
        if (_formats.Any(f => f.Codec == format.Codec && f.FormatID == format.FormatID))
        {
            _selected = format;
        }
    }

    public void GotVideoFrame(IPEndPoint remoteEndPoint, uint rtpTimestamp, byte[] payload, VideoFormat format)
    {
        if (payload is null || payload.Length == 0)
        {
            return;
        }

        if (format.Codec != VideoCodecsEnum.VP8 && _selected.Codec != VideoCodecsEnum.VP8)
        {
            return;
        }

        var attempts = Interlocked.Increment(ref _decodeAttempts);
        if (attempts == 1 || attempts % 100 == 0)
        {
            OnDiagnostic?.Invoke($"VP8 decode attempt #{attempts}: payloadBytes={payload.Length}, format={format.Codec}/{format.FormatID}.");
        }

        // SIPSorcery's OnVideoFrameReceived is expected to provide a complete encoded frame,
        // but in case the VP8 payload descriptor is still present, attempt decode twice.
        if (TryDecodeAndRaise(rtpTimestamp, payload, descriptorStripped: false))
        {
            return;
        }

        if (TryStripVp8PayloadDescriptor(payload, out var stripped))
        {
            Interlocked.Increment(ref _descriptorStrips);
            if (TryDecodeAndRaise(rtpTimestamp, stripped, descriptorStripped: true))
            {
                return;
            }
        }
    }

    public void Dispose()
    {
        _codec.Dispose();
    }

    private bool TryDecodeAndRaise(uint rtpTimestamp, byte[] encoded, bool descriptorStripped)
    {
        try
        {
            foreach (var decoded in _codec.DecodeVideo(encoded, VideoPixelFormatsEnum.Bgr, VideoCodecsEnum.VP8))
            {
                _ = checked((int)decoded.Width * 3);
                OnDecodedFrame?.Invoke(rtpTimestamp, (int)decoded.Width, (int)decoded.Height, decoded.Sample, VideoPixelFormatsEnum.Bgr);
            }

            var ok = Interlocked.Increment(ref _decodeSuccess);
            if (ok == 1 || ok % 100 == 0)
            {
                OnDiagnostic?.Invoke($"VP8 decode success #{ok}: encodedBytes={encoded.Length}, stripped={descriptorStripped}.");
            }

            return true;
        }
        catch
        {
            var fail = Interlocked.Increment(ref _decodeFailures);
            if (fail == 1 || fail % 50 == 0)
            {
                var strips = Interlocked.Read(ref _descriptorStrips);
                OnDiagnostic?.Invoke($"VP8 decode failed #{fail}: encodedBytes={encoded.Length}, stripped={descriptorStripped}, totalStrips={strips}.");
            }

            return false;
        }
    }

    // RFC 7741: VP8 payload descriptor (minimal parsing).
    private static bool TryStripVp8PayloadDescriptor(byte[] payload, out byte[] stripped)
    {
        stripped = Array.Empty<byte>();
        if (payload.Length < 2)
        {
            return false;
        }

        var idx = 0;
        var b0 = payload[idx++];
        var x = (b0 & 0x80) != 0;
        if (x)
        {
            if (idx >= payload.Length)
            {
                return false;
            }

            var b1 = payload[idx++];
            var i = (b1 & 0x80) != 0;
            var l = (b1 & 0x40) != 0;
            var t = (b1 & 0x20) != 0;
            var k = (b1 & 0x10) != 0;

            if (i)
            {
                if (idx >= payload.Length)
                {
                    return false;
                }

                var picId = payload[idx++];
                if ((picId & 0x80) != 0)
                {
                    if (idx >= payload.Length)
                    {
                        return false;
                    }

                    idx++;
                }
            }

            if (l)
            {
                if (idx >= payload.Length)
                {
                    return false;
                }

                idx++;
            }

            if (t || k)
            {
                if (idx >= payload.Length)
                {
                    return false;
                }

                idx++;
            }
        }

        if (idx <= 0 || idx >= payload.Length)
        {
            return false;
        }

        stripped = new byte[payload.Length - idx];
        Buffer.BlockCopy(payload, idx, stripped, 0, stripped.Length);
        return true;
    }
}
