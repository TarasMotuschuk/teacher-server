using System.Net;
using System.Runtime.InteropServices;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Encoders;
using Vortice.MediaFoundation;
using VideoFormat = SIPSorceryMedia.Abstractions.VideoFormat;

namespace StudentAgent.UIHost.Services;

/// <summary>
/// Demo-only receive path: supports VP8 (via SIPSorceryMedia.Encoders/libvpx) and H.264 (Media Foundation on Windows).
/// </summary>
public sealed class DemoWebRtcVideoReceiveEndPoint : IDisposable
{
    private static readonly Guid ClsidCmsH264DecoderMft = new("62CE7E72-4C71-4D20-B15D-452831A87D9D");

    // IID of IMFTransform (mftransform.h). Hard-coded so we don't depend on whether
    // Vortice's generated IMFTransform class exposes the right [Guid] metadata.
    private static readonly Guid IidImfTransform = new("BF94C121-5B05-4E6F-8000-BA598961414D");

    private const uint CLSCTX_INPROC_SERVER = 0x1;
    private const int H264FormatId = 100;

    private readonly object _sync = new();
    private readonly List<VideoFormat> _formats = [];
    private VideoFormat _selected;

    private readonly VpxVideoEncoder _vp8Codec = new();
    private long _vp8DecodeAttempts;
    private long _vp8DecodeSuccess;
    private long _vp8DecodeFailures;
    private long _vp8DescriptorStrips;

    private bool _mfStarted;
    private IMFTransform? _h264Decoder;
    private int _h264InputStreamId;
    private int _h264OutputStreamId;
    private int _nv12Width;
    private int _nv12Height;
    private long _h264DecodeAttempts;
    private long _h264DecodeSuccess;
    private long _h264DecodeFailures;
    private long _h264ProcessInputCalls;
    private long _h264NeedMoreInput;
    private long _h264StreamChanges;
    private long _h264OutputSamples;
    private long _h264NvConvertFailures;

    public DemoWebRtcVideoReceiveEndPoint()
    {
        _formats.Add(new VideoFormat(VideoCodecsEnum.VP8, VpxVideoEncoder.VP8_FORMATID));
        _formats.Add(new VideoFormat(VideoCodecsEnum.H264, H264FormatId, 90000, string.Empty));
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
        _ = remoteEndPoint;

        if (payload is null || payload.Length == 0)
        {
            return;
        }

        if (format.Codec == VideoCodecsEnum.H264 || _selected.Codec == VideoCodecsEnum.H264)
        {
            TryDecodeH264(rtpTimestamp, payload);
            return;
        }

        if (format.Codec == VideoCodecsEnum.VP8 || _selected.Codec == VideoCodecsEnum.VP8)
        {
            TryDecodeVp8(rtpTimestamp, payload);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            TeardownH264DecoderLocked();
        }

        _vp8Codec.Dispose();
    }

    private void TryDecodeVp8(uint rtpTimestamp, byte[] payload)
    {
        var attempts = Interlocked.Increment(ref _vp8DecodeAttempts);
        if (attempts == 1 || attempts % 100 == 0)
        {
            OnDiagnostic?.Invoke($"VP8 decode attempt #{attempts}: payloadBytes={payload.Length}.");
        }

        if (TryDecodeVp8AndRaise(rtpTimestamp, payload, descriptorStripped: false))
        {
            return;
        }

        if (TryStripVp8PayloadDescriptor(payload, out var stripped))
        {
            Interlocked.Increment(ref _vp8DescriptorStrips);
            if (TryDecodeVp8AndRaise(rtpTimestamp, stripped, descriptorStripped: true))
            {
                return;
            }
        }

        var fail = Interlocked.Increment(ref _vp8DecodeFailures);
        if (fail == 1 || fail % 50 == 0)
        {
            OnDiagnostic?.Invoke($"VP8 decode failed #{fail}: payloadBytes={payload.Length}.");
        }
    }

    private bool TryDecodeVp8AndRaise(uint rtpTimestamp, byte[] encoded, bool descriptorStripped)
    {
        try
        {
            foreach (var decoded in _vp8Codec.DecodeVideo(encoded, VideoPixelFormatsEnum.Bgr, VideoCodecsEnum.VP8))
            {
                _ = checked((int)decoded.Width * 3);
                OnDecodedFrame?.Invoke(rtpTimestamp, (int)decoded.Width, (int)decoded.Height, decoded.Sample, VideoPixelFormatsEnum.Bgr);
            }

            var ok = Interlocked.Increment(ref _vp8DecodeSuccess);
            if (ok == 1 || ok % 100 == 0)
            {
                OnDiagnostic?.Invoke($"VP8 decode success #{ok}: encodedBytes={encoded.Length}, stripped={descriptorStripped}.");
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void TryDecodeH264(uint rtpTimestamp, byte[] payload)
    {
        var attempts = Interlocked.Increment(ref _h264DecodeAttempts);
        if (attempts == 1 || attempts % 100 == 0)
        {
            OnDiagnostic?.Invoke($"H264 decode attempt #{attempts}: payloadBytes={payload.Length}.");
        }

        try
        {
            lock (_sync)
            {
                EnsureMediaFoundationStartedLocked();
                EnsureH264DecoderLocked();

                if (_h264Decoder is null)
                {
                    return;
                }

                using var inputSample = CreateSampleFromBytes(payload);
                _h264Decoder.ProcessInput(_h264InputStreamId, inputSample, 0);
                var inputCalls = Interlocked.Increment(ref _h264ProcessInputCalls);
                if (inputCalls == 1)
                {
                    OnDiagnostic?.Invoke($"H264 ProcessInput #1 accepted: payloadBytes={payload.Length}.");
                }

                while (true)
                {
                    var buffer = new OutputDataBuffer
                    {
                        StreamID = _h264OutputStreamId,
                    };

                    var hr = _h264Decoder.ProcessOutput(ProcessOutputFlags.None, 1, ref buffer, out _);
                    using var outSample = buffer.Sample;

                    if (hr == ResultCode.TransformNeedMoreInput)
                    {
                        var nmi = Interlocked.Increment(ref _h264NeedMoreInput);
                        if (nmi == 1 || nmi % 100 == 0)
                        {
                            OnDiagnostic?.Invoke($"H264 ProcessOutput: NEED_MORE_INPUT #{nmi} (inputCalls={inputCalls}).");
                        }

                        return;
                    }

                    if (hr == ResultCode.TransformStreamChange)
                    {
                        ConfigureH264DecoderOutputTypeLocked();
                        var sc = Interlocked.Increment(ref _h264StreamChanges);
                        OnDiagnostic?.Invoke($"H264 ProcessOutput: STREAM_CHANGE #{sc}, nv12={_nv12Width}x{_nv12Height}.");
                        continue;
                    }

                    if (hr.Failure)
                    {
                        throw new InvalidOperationException($"H264 ProcessOutput failed: {hr} (inputCalls={inputCalls}).");
                    }

                    if (outSample is null)
                    {
                        continue;
                    }

                    var outCount = Interlocked.Increment(ref _h264OutputSamples);
                    if (outCount == 1)
                    {
                        OnDiagnostic?.Invoke($"H264 ProcessOutput: first output sample emitted (nv12={_nv12Width}x{_nv12Height}).");
                    }

                    if (_nv12Width <= 0 || _nv12Height <= 0)
                    {
                        continue;
                    }

                    if (!TryGetBgr24FromNv12Sample(outSample, _nv12Width, _nv12Height, out var bgr) || bgr.Length == 0)
                    {
                        var nv = Interlocked.Increment(ref _h264NvConvertFailures);
                        if (nv == 1 || nv % 50 == 0)
                        {
                            OnDiagnostic?.Invoke($"H264 NV12->BGR conversion failed #{nv} ({_nv12Width}x{_nv12Height}).");
                        }

                        continue;
                    }

                    OnDecodedFrame?.Invoke(rtpTimestamp, _nv12Width, _nv12Height, bgr, VideoPixelFormatsEnum.Bgr);
                    var ok = Interlocked.Increment(ref _h264DecodeSuccess);
                    if (ok == 1 || ok % 100 == 0)
                    {
                        OnDiagnostic?.Invoke($"H264 decode success #{ok}: {_nv12Width}x{_nv12Height}, bytes={bgr.Length}.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            var fail = Interlocked.Increment(ref _h264DecodeFailures);
            if (fail == 1 || fail % 50 == 0)
            {
                OnDiagnostic?.Invoke($"H264 decode failed #{fail}: {ex.Message}");
            }
        }
    }

    private void EnsureMediaFoundationStartedLocked()
    {
        if (_mfStarted)
        {
            return;
        }

        MediaFactory.MFStartup(useLightVersion: false);
        _mfStarted = true;
    }

    private void EnsureH264DecoderLocked()
    {
        if (_h264Decoder is not null)
        {
            return;
        }

        // Activator.CreateInstance(Type.GetTypeFromCLSID(...)) returns a generic
        // System.__ComObject RCW which the CLR refuses to cast to Vortice's
        // SharpGen-generated IMFTransform class (that class is not [ComImport]).
        // Call CoCreateInstance directly, asking for IID_IMFTransform, and wrap the
        // resulting native pointer via the SharpGen.Runtime.ComObject(IntPtr) ctor.
        var clsid = ClsidCmsH264DecoderMft;
        var iid = IidImfTransform;
        var hr = CoCreateInstance(ref clsid, IntPtr.Zero, CLSCTX_INPROC_SERVER, ref iid, out var nativeTransform);
        if (hr != 0 || nativeTransform == IntPtr.Zero)
        {
            throw new InvalidOperationException($"CoCreateInstance(CMS H264 Decoder MFT, IID_IMFTransform) failed: 0x{hr:X8}");
        }

        _h264Decoder = new IMFTransform(nativeTransform);

        _h264Decoder.ProcessMessage(TMessageType.MessageNotifyBeginStreaming, UIntPtr.Zero);
        _h264Decoder.ProcessMessage(TMessageType.MessageNotifyStartOfStream, UIntPtr.Zero);

        _h264Decoder.GetStreamCount(out var inputs, out var outputs);
        if (inputs != 1 || outputs != 1)
        {
            throw new InvalidOperationException($"Unexpected H264 MFT stream layout: inputs={inputs}, outputs={outputs}.");
        }

        _h264InputStreamId = 0;
        _h264OutputStreamId = 0;

        using (var inputType = MediaFactory.MFCreateMediaType())
        {
            inputType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
            inputType.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.H264);
            _h264Decoder.SetInputType(_h264InputStreamId, inputType, 0);
        }

        ConfigureH264DecoderOutputTypeLocked();
    }

    private void ConfigureH264DecoderOutputTypeLocked()
    {
        if (_h264Decoder is null)
        {
            return;
        }

        IMFMediaType? chosen = null;
        for (var i = 0; ; i++)
        {
            var candidate = _h264Decoder.GetOutputAvailableType(_h264OutputStreamId, i);
            if (candidate is null)
            {
                break;
            }

            candidate.GetGUID(MediaTypeAttributeKeys.Subtype, out Guid subtype);
            if (subtype == VideoFormatGuids.NV12)
            {
                chosen = candidate;
                break;
            }

            candidate.Dispose();
        }

        chosen ??= _h264Decoder.GetOutputAvailableType(_h264OutputStreamId, 0)
            ?? throw new InvalidOperationException("H264 decoder did not offer an output media type.");

        if (!TryUnpackFrameSize(chosen, out var width, out var height))
        {
            using var currentOut = _h264Decoder.GetOutputCurrentType(_h264OutputStreamId);
            if (currentOut is not null && TryUnpackFrameSize(currentOut, out width, out height))
            {
                // keep
            }
        }

        _nv12Width = width;
        _nv12Height = height;

        _h264Decoder.SetOutputType(_h264OutputStreamId, chosen, 0);
        chosen.Dispose();
    }

    private static bool TryUnpackFrameSize(IMFMediaType mediaType, out int width, out int height)
    {
        width = 0;
        height = 0;

        var hr = mediaType.GetUInt64(MediaTypeAttributeKeys.FrameSize, out var packed);
        if (hr.Failure)
        {
            return false;
        }

        // MF_MT_FRAME_SIZE is a UINT64: (width << 32) | height.
        width = (int)(packed >> 32);
        height = (int)(packed & 0xFFFF_FFFF);
        return width > 0 && height > 0;
    }

    private void TeardownH264DecoderLocked()
    {
        _nv12Width = 0;
        _nv12Height = 0;

        if (_h264Decoder is not null)
        {
            try
            {
                _h264Decoder.ProcessMessage(TMessageType.MessageNotifyEndOfStream, UIntPtr.Zero);
                _h264Decoder.ProcessMessage(TMessageType.MessageNotifyEndStreaming, UIntPtr.Zero);
            }
            catch
            {
            }

            _h264Decoder.Dispose();
            _h264Decoder = null;
        }

        if (_mfStarted)
        {
            try
            {
                MediaFactory.MFShutdown();
            }
            catch
            {
            }

            _mfStarted = false;
        }
    }

    private static IMFSample CreateSampleFromBytes(byte[] payload)
    {
        var buffer = MediaFactory.MFCreateMemoryBuffer(payload.Length);
        buffer.Lock(out var ptr, out _, out _);
        try
        {
            Marshal.Copy(payload, 0, ptr, payload.Length);
        }
        finally
        {
            buffer.Unlock();
        }

        buffer.CurrentLength = payload.Length;

        var sample = MediaFactory.MFCreateSample();
        sample.AddBuffer(buffer);
        return sample;
    }

    private static bool TryGetBgr24FromNv12Sample(IMFSample sample, int width, int height, out byte[] bgr)
    {
        bgr = [];

        var buffer = sample.GetBufferByIndex(0);
        if (buffer is null)
        {
            return false;
        }

        using (buffer)
        {
            var twoD = buffer.QueryInterfaceOrNull<IMF2DBuffer>();
            if (twoD is not null)
            {
                using (twoD)
                {
                    twoD.Lock2D(out var scan0, out var pitch);
                    try
                    {
                        bgr = new byte[checked(width * height * 3)];
                        CopyNv12ToBgr(scan0, pitch, width, height, bgr);
                        return true;
                    }
                    finally
                    {
                        twoD.Unlock2D();
                    }
                }
            }

            buffer.Lock(out var ptr, out _, out var currentLen);
            try
            {
                var expected = (int)(width * height * 3L / 2);
                if (currentLen < expected)
                {
                    return false;
                }

                bgr = new byte[checked(width * height * 3)];
                CopyNv12ToBgr(ptr, width, width, height, bgr);
                return true;
            }
            finally
            {
                buffer.Unlock();
            }
        }
    }

    private static void CopyNv12ToBgr(nint basePtr, int yStride, int width, int height, byte[] bgr)
    {
        var uvOffset = yStride * height;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var yVal = Marshal.ReadByte(basePtr + (y * yStride) + x);
                var uvRow = (y / 2) * yStride;
                var uvCol = (x / 2) * 2;
                var u = Marshal.ReadByte(basePtr + uvOffset + uvRow + uvCol);
                var v = Marshal.ReadByte(basePtr + uvOffset + uvRow + uvCol + 1);

                var c = yVal - 16;
                var d = u - 128;
                var e = v - 128;

                var r = (298 * c + 409 * e + 128) >> 8;
                var g = (298 * c - 100 * d - 208 * e + 128) >> 8;
                var b = (298 * c + 516 * d + 128) >> 8;

                r = Math.Clamp(r, 0, 255);
                g = Math.Clamp(g, 0, 255);
                b = Math.Clamp(b, 0, 255);

                var o = (y * width * 3) + (x * 3);
                bgr[o + 0] = (byte)b;
                bgr[o + 1] = (byte)g;
                bgr[o + 2] = (byte)r;
            }
        }
    }

    [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int CoCreateInstance(
        ref Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        ref Guid riid,
        out IntPtr ppv);

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
