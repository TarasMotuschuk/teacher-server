using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SIPSorceryMedia.Abstractions;

namespace TeacherClient.CrossPlatform.Services;

/// <summary>
/// macOS H.264 encoder using VideoToolbox (hardware where available). Produces Annex B elementary stream chunks suitable for SIPSorcery's H264 packetizer.
/// </summary>
public sealed class VideoToolboxH264VideoEncoder : IVideoEncoder, IDisposable
{
    // Keep aligned with SIPSorcery's suggested H.264 dynamic payload type for WebRTC samples in this repo.
    private const int H264FormatId = 100;

    private const string DefaultH264Fmtp =
        "packetization-mode=1;profile-level-id=42e01f;level-asymmetry-allowed=1";

    private readonly object _sync = new();
    private readonly List<VideoFormat> _supportedFormats =
    [
        new VideoFormat(VideoCodecsEnum.H264, H264FormatId, 90000, DefaultH264Fmtp),
    ];

    private IntPtr _compressionSession;
    private int _lastWidth;
    private int _lastHeight;
    private readonly int _requestedFps;
    private int _timebaseFps;
    private long _frameIndex;
    private bool _forceKeyFrame;
    private bool _disposed;

    private IntPtr _outputCallbackFn;
    private VTCompressionOutputDelegate? _outputCallback;

    public VideoToolboxH264VideoEncoder(int captureFps = 15)
    {
        _requestedFps = Math.Clamp(captureFps, 1, 60);
    }

    public List<VideoFormat> SupportedFormats => _supportedFormats;

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            TeardownSessionLocked();
        }

        GC.SuppressFinalize(this);
    }

    public byte[] EncodeVideo(int width, int height, byte[] sample, VideoPixelFormatsEnum pixelFormat, VideoCodecsEnum codec)
    {
        if (codec != VideoCodecsEnum.H264)
        {
            throw new NotSupportedException($"Unsupported codec {codec}.");
        }

        if (pixelFormat != VideoPixelFormatsEnum.Bgra)
        {
            throw new NotSupportedException($"Unsupported pixel format {pixelFormat}. Expected BGRA.");
        }

        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException($"{nameof(VideoToolboxH264VideoEncoder)} is only supported on macOS.");
        }

        if (width <= 0 || height <= 0)
        {
            return [];
        }

        var tightStride = width * 4;
        var expected = checked(tightStride * height);
        if (sample.Length < expected)
        {
            return [];
        }

        lock (_sync)
        {
            if (_disposed)
            {
                return [];
            }

            EnsureOutputCallbackLocked();
            EnsureSessionLocked(width, height);

            using var output = new EncodedFrameAccumulator();
            var reg = GCHandle.Alloc(output);
            try
            {
                var frameUserData = GCHandle.ToIntPtr(reg);

                var pinned = GCHandle.Alloc(sample, GCHandleType.Pinned);
                try
                {
                    var basePtr = pinned.AddrOfPinnedObject();

                    var pbStatus = CVPixelBufferCreateWithBytes(
                        IntPtr.Zero,
                        (nuint)width,
                        (nuint)height,
                        kCVPixelFormatType_32BGRA,
                        basePtr,
                        (nuint)tightStride,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        out var pixelBuffer);
                    if (pbStatus != 0)
                    {
                        throw new InvalidOperationException($"CVPixelBufferCreateWithBytes failed: {pbStatus}.");
                    }

                    try
                    {
                        var pts = CMTimeMake(Interlocked.Increment(ref _frameIndex) - 1, _timebaseFps);
                        var flags = _forceKeyFrame ? VTEncodeInfoFlags.kVTEncodeInfo_FrameIsForcedKeyFrame : 0;
                        _forceKeyFrame = false;

                        var st = VTCompressionSessionEncodeFrame(
                            _compressionSession,
                            pts,
                            pixelBuffer,
                            flags,
                            frameUserData,
                            out _);
                        if (st != 0)
                        {
                            throw new InvalidOperationException($"VTCompressionSessionEncodeFrame failed: {st}.");
                        }

                    }
                    finally
                    {
                        CVPixelBufferRelease(pixelBuffer);
                    }
                }
                finally
                {
                    pinned.Free();
                }
            }
            finally
            {
                reg.Free();
            }

            return output.ToArray();
        }
    }

    public byte[] EncodeVideoFaster(RawImage rawImage, VideoCodecsEnum codec) =>
        EncodeVideo(rawImage.Width, rawImage.Height, rawImage.GetBuffer(), rawImage.PixelFormat, codec);

    public IEnumerable<VideoSample> DecodeVideo(byte[] encodedSample, VideoPixelFormatsEnum pixelFormat, VideoCodecsEnum codec) =>
        throw new NotSupportedException();

    public IEnumerable<RawImage> DecodeVideoFaster(byte[] encodedSample, VideoPixelFormatsEnum pixelFormat, VideoCodecsEnum codec) =>
        throw new NotSupportedException();

    public void ForceKeyFrame()
    {
        lock (_sync)
        {
            _forceKeyFrame = true;
        }
    }

    private void EnsureOutputCallbackLocked()
    {
        if (_outputCallbackFn != IntPtr.Zero)
        {
            return;
        }

        _outputCallback = OutputShim;
        _outputCallbackFn = Marshal.GetFunctionPointerForDelegate(_outputCallback);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void VTCompressionOutputDelegate(
        IntPtr outputCallbackRefCon,
        IntPtr sourceFrameRefCon,
        int status,
        VTEncodeInfoFlags infoFlags,
        IntPtr sampleBuffer);

    private static void OutputShim(
        IntPtr outputCallbackRefCon,
        IntPtr sourceFrameRefCon,
        int status,
        VTEncodeInfoFlags infoFlags,
        IntPtr sampleBuffer)
    {
        _ = sourceFrameRefCon;

        if (outputCallbackRefCon == IntPtr.Zero || sampleBuffer == IntPtr.Zero || status != 0)
        {
            return;
        }

        var handle = GCHandle.FromIntPtr(outputCallbackRefCon);
        if (!handle.IsAllocated || handle.Target is not EncodedFrameAccumulator acc)
        {
            return;
        }

        try
        {
            AppendAnnexB(sampleBuffer, acc);
        }
        catch
        {
        }
    }

    private void EnsureSessionLocked(int width, int height)
    {
        if (_compressionSession != IntPtr.Zero && width == _lastWidth && height == _lastHeight)
        {
            return;
        }

        TeardownSessionLocked();

        _lastWidth = width;
        _lastHeight = height;
        _timebaseFps = _requestedFps;
        _frameIndex = 0;

        var encoderSpec = CreateEncoderSpecificationPreferHardware();
        var status = VTCompressionSessionCreate(
            IntPtr.Zero,
            width,
            height,
            kCMVideoCodecType_H264,
            encoderSpec,
            IntPtr.Zero,
            IntPtr.Zero,
            _outputCallbackFn,
            IntPtr.Zero,
            out var session);
        if (encoderSpec != IntPtr.Zero)
        {
            CFRelease(encoderSpec);
        }

        if (status != 0)
        {
            throw new InvalidOperationException($"VTCompressionSessionCreate failed: {status}.");
        }

        _compressionSession = session;

        SetNumber(_compressionSession, "RealTime", 1);
        SetNumber(_compressionSession, "AllowFrameReordering", 0);
        SetNumber(_compressionSession, "MaxKeyFrameInterval", 60);
        SetNumber(_compressionSession, "AverageBitRate", 2_500_000);
        SetString(_compressionSession, "ProfileLevel", "H264_Baseline_AutoLevel");

        var prep = VTCompressionSessionPrepareToEncodeFrames(_compressionSession);
        if (prep != 0)
        {
            throw new InvalidOperationException($"VTCompressionSessionPrepareToEncodeFrames failed: {prep}.");
        }
    }

    private void TeardownSessionLocked()
    {
        if (_compressionSession != IntPtr.Zero)
        {
            VTCompressionSessionInvalidate(_compressionSession);
            VTCompressionSessionRelease(_compressionSession);
            _compressionSession = IntPtr.Zero;
        }

        _lastWidth = 0;
        _lastHeight = 0;
        _timebaseFps = 0;
        _frameIndex = 0;
    }

    private static IntPtr CreateEncoderSpecificationPreferHardware()
    {
        var key = CFStringCreateWithCString(IntPtr.Zero, "EnableHardwareAcceleratedVideoEncoder", kCFStringEncodingUTF8);
        var val = CreateCfInt32(1);
        if (key == IntPtr.Zero || val == IntPtr.Zero)
        {
            if (key != IntPtr.Zero)
            {
                CFRelease(key);
            }

            if (val != IntPtr.Zero)
            {
                CFRelease(val);
            }

            return IntPtr.Zero;
        }

        var keys = new[] { key };
        var vals = new[] { val };
        var spec = CFDictionaryCreate(IntPtr.Zero, keys, vals, 1, IntPtr.Zero, IntPtr.Zero);
        CFRelease(key);
        CFRelease(val);
        return spec;
    }

    private static IntPtr CreateCfInt32(int value)
    {
        var v = value;
        var p = Marshal.AllocHGlobal(sizeof(int));
        Marshal.WriteInt32(p, v);
        var n = CFNumberCreate(IntPtr.Zero, CFNumberType.kCFNumberSInt32Type, p);
        Marshal.FreeHGlobal(p);
        return n;
    }

    private static void SetNumber(IntPtr session, string keyName, int value)
    {
        var key = CFStringCreateWithCString(IntPtr.Zero, keyName, kCFStringEncodingUTF8);
        var val = CreateCfInt32(value);
        if (key == IntPtr.Zero || val == IntPtr.Zero)
        {
            if (key != IntPtr.Zero)
            {
                CFRelease(key);
            }

            if (val != IntPtr.Zero)
            {
                CFRelease(val);
            }

            return;
        }

        _ = VTSessionSetProperty(session, key, val);
        CFRelease(key);
        CFRelease(val);
    }

    private static void SetString(IntPtr session, string keyName, string value)
    {
        var key = CFStringCreateWithCString(IntPtr.Zero, keyName, kCFStringEncodingUTF8);
        var val = CFStringCreateWithCString(IntPtr.Zero, value, kCFStringEncodingUTF8);
        if (key == IntPtr.Zero || val == IntPtr.Zero)
        {
            if (key != IntPtr.Zero)
            {
                CFRelease(key);
            }

            if (val != IntPtr.Zero)
            {
                CFRelease(val);
            }

            return;
        }

        _ = VTSessionSetProperty(session, key, val);
        CFRelease(key);
        CFRelease(val);
    }

    private static void AppendAnnexB(IntPtr sampleBuffer, EncodedFrameAccumulator acc)
    {
        var dataBuffer = CMSampleBufferGetDataBuffer(sampleBuffer);
        if (dataBuffer == IntPtr.Zero)
        {
            return;
        }

        var totalLen = CMBlockBufferGetDataLength(dataBuffer);
        if (totalLen <= 0)
        {
            return;
        }

        var tmp = new byte[totalLen];
        var tmpHandle = GCHandle.Alloc(tmp, GCHandleType.Pinned);
        try
        {
            var copy = CMBlockBufferCopyDataBytes(dataBuffer, 0, (nuint)totalLen, tmpHandle.AddrOfPinnedObject());
            if (copy != 0)
            {
                return;
            }
        }
        finally
        {
            tmpHandle.Free();
        }

        if (TryConvertAvccToAnnexB(tmp, acc.Stream))
        {
            return;
        }

        acc.Stream.Write(tmp);
    }

    private static bool TryConvertAvccToAnnexB(ReadOnlySpan<byte> avcc, MemoryStream dst)
    {
        if (avcc.Length < 4)
        {
            return false;
        }

        if (avcc.Length >= 4 && avcc[0] == 0x00 && avcc[1] == 0x00 && (avcc[2] == 0x01 || (avcc[2] == 0x00 && avcc[3] == 0x01)))
        {
            return false;
        }

        var idx = 0;
        while (idx + 4 <= avcc.Length)
        {
            var nalLen = (int)BinaryPrimitives.ReadUInt32BigEndian(avcc.Slice(idx, 4));
            idx += 4;
            if (nalLen <= 0 || idx + nalLen > avcc.Length)
            {
                return false;
            }

            dst.WriteByte(0x00);
            dst.WriteByte(0x00);
            dst.WriteByte(0x00);
            dst.WriteByte(0x01);
            dst.Write(avcc.Slice(idx, nalLen));
            idx += nalLen;
        }

        return idx == avcc.Length;
    }

    private sealed class EncodedFrameAccumulator : IDisposable
    {
        public MemoryStream Stream { get; } = new();

        public byte[] ToArray() => Stream.Length == 0 ? [] : Stream.ToArray();

        public void Dispose()
        {
            Stream.Dispose();
        }
    }

    private enum VTEncodeInfoFlags
    {
        kVTEncodeInfo_FrameIsForcedKeyFrame = 1 << 3,
    }

    private enum CFNumberType
    {
        kCFNumberSInt32Type = 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CMTime
    {
        public CMTime(long value, int timescale, uint flags, long epoch)
        {
            Value = value;
            Timescale = timescale;
            Flags = flags;
            Epoch = epoch;
        }

        public long Value { get; }
        public int Timescale { get; }
        public uint Flags { get; }
        public long Epoch { get; }
    }

    private const uint kCVPixelFormatType_32BGRA = 0x42475241; // 'BGRA'
    private const uint kCMVideoCodecType_H264 = 0x61766331; // 'avc1'

    private const int kCFStringEncodingUTF8 = 0x08000100;

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string cStr, int encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFNumberCreate(IntPtr allocator, CFNumberType theType, IntPtr valuePtr);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFDictionaryCreate(IntPtr allocator, IntPtr[] keys, IntPtr[] values, nint numValues, IntPtr keyCallBacks, IntPtr valueCallBacks);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);

    [DllImport("/System/Library/Frameworks/CoreMedia.framework/CoreMedia")]
    private static extern CMTime CMTimeMake(long value, int timescale);

    [DllImport("/System/Library/Frameworks/CoreMedia.framework/CoreMedia")]
    private static extern IntPtr CMSampleBufferGetDataBuffer(IntPtr sbuf);

    [DllImport("/System/Library/Frameworks/CoreMedia.framework/CoreMedia")]
    private static extern int CMBlockBufferGetDataLength(IntPtr blockBuffer);

    [DllImport("/System/Library/Frameworks/CoreMedia.framework/CoreMedia")]
    private static extern int CMBlockBufferCopyDataBytes(IntPtr blockBuffer, nuint offset, nuint length, IntPtr dataPointer);

    [DllImport("/System/Library/Frameworks/CoreVideo.framework/CoreVideo")]
    private static extern int CVPixelBufferCreateWithBytes(
        IntPtr allocator,
        nuint width,
        nuint height,
        uint pixelFormatType,
        IntPtr baseAddress,
        nuint bytesPerRow,
        IntPtr releaseCallback,
        IntPtr releaseRefCon,
        IntPtr pixelBufferAttributes,
        out IntPtr pixelBufferOut);

    [DllImport("/System/Library/Frameworks/CoreVideo.framework/CoreVideo")]
    private static extern void CVPixelBufferRelease(IntPtr pixelBuffer);

    [DllImport("/System/Library/Frameworks/VideoToolbox.framework/VideoToolbox")]
    private static extern int VTCompressionSessionCreate(
        IntPtr allocator,
        int width,
        int height,
        uint codecType,
        IntPtr encoderSpecification,
        IntPtr sourceImageBufferAttributes,
        IntPtr compressedDataAllocator,
        IntPtr outputCallback,
        IntPtr outputCallbackRefCon,
        out IntPtr compressionSessionOut);

    [DllImport("/System/Library/Frameworks/VideoToolbox.framework/VideoToolbox")]
    private static extern int VTCompressionSessionPrepareToEncodeFrames(IntPtr session);

    [DllImport("/System/Library/Frameworks/VideoToolbox.framework/VideoToolbox")]
    private static extern int VTCompressionSessionEncodeFrame(
        IntPtr session,
        CMTime presentationTimeStamp,
        IntPtr imageBuffer,
        VTEncodeInfoFlags encodeFlags,
        IntPtr sourceFrameRefCon,
        out VTEncodeInfoFlags infoFlagsOut);

    [DllImport("/System/Library/Frameworks/VideoToolbox.framework/VideoToolbox")]
    private static extern void VTCompressionSessionInvalidate(IntPtr session);

    [DllImport("/System/Library/Frameworks/VideoToolbox.framework/VideoToolbox")]
    private static extern void VTCompressionSessionRelease(IntPtr session);

    [DllImport("/System/Library/Frameworks/VideoToolbox.framework/VideoToolbox")]
    private static extern int VTSessionSetProperty(IntPtr session, IntPtr propertyKey, IntPtr propertyValue);
}
