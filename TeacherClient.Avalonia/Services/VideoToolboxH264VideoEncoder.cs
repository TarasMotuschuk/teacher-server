using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using SIPSorceryMedia.Abstractions;

namespace TeacherClient.CrossPlatform.Services;

/// <summary>
/// macOS H.264 encoder backed by VideoToolbox. Consumes tightly-packed BGRA frames
/// and emits an Annex-B elementary stream (with SPS/PPS prepended on keyframes)
/// suitable for SIPSorcery's H264 RTP packetizer on the student side.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class VideoToolboxH264VideoEncoder : IVideoEncoder, IDisposable
{
    private const int H264FormatId = 100;

    private const string DefaultH264Fmtp =
        "packetization-mode=1;profile-level-id=42e01f;level-asymmetry-allowed=1";

    private static readonly byte[] AnnexBStartCode = [0x00, 0x00, 0x00, 0x01];
    private static readonly Lazy<VideoToolboxConstants> SharedConstants = new(VideoToolboxConstants.Resolve);

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
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException($"{nameof(VideoToolboxH264VideoEncoder)} requires macOS.");
        }

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

            using var frameContext = new FrameEncodeContext();
            var reg = GCHandle.Alloc(frameContext);
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
                        var duration = CMTimeMake(1, _timebaseFps);

                        var framePropsDict = IntPtr.Zero;
                        if (_forceKeyFrame)
                        {
                            framePropsDict = CreateForceKeyFrameFrameProperties();
                            _forceKeyFrame = false;
                        }

                        try
                        {
                            var st = VTCompressionSessionEncodeFrame(
                                _compressionSession,
                                pixelBuffer,
                                pts,
                                duration,
                                framePropsDict,
                                frameUserData,
                                out _);
                            if (st != 0)
                            {
                                throw new InvalidOperationException($"VTCompressionSessionEncodeFrame failed: {st}.");
                            }
                        }
                        finally
                        {
                            if (framePropsDict != IntPtr.Zero)
                            {
                                CFRelease(framePropsDict);
                            }
                        }

                        var completeStatus = VTCompressionSessionCompleteFrames(_compressionSession, pts);
                        if (completeStatus != 0)
                        {
                            throw new InvalidOperationException($"VTCompressionSessionCompleteFrames failed: {completeStatus}.");
                        }

                        _ = frameContext.WaitForCompletion(TimeSpan.FromSeconds(2));
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

            return frameContext.ToArray();
        }
    }

    public byte[] EncodeVideoFaster(RawImage rawImage, VideoCodecsEnum codec) =>
        EncodeVideo(rawImage.Width, rawImage.Height, rawImage.GetBuffer(), rawImage.PixelFormat, codec);

    public IEnumerable<VideoSample> DecodeVideo(byte[] encodedSample, VideoPixelFormatsEnum pixelFormat, VideoCodecsEnum codec) =>
        throw new NotSupportedException("VideoToolboxH264VideoEncoder is encode-only; decode is handled on the student agent.");

    public IEnumerable<RawImage> DecodeVideoFaster(byte[] encodedSample, VideoPixelFormatsEnum pixelFormat, VideoCodecsEnum codec) =>
        throw new NotSupportedException("VideoToolboxH264VideoEncoder is encode-only; decode is handled on the student agent.");

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
        _ = outputCallbackRefCon;
        _ = infoFlags;

        if (sourceFrameRefCon == IntPtr.Zero)
        {
            return;
        }

        var handle = GCHandle.FromIntPtr(sourceFrameRefCon);
        if (!handle.IsAllocated || handle.Target is not FrameEncodeContext frameContext)
        {
            return;
        }

        try
        {
            frameContext.Status = status;
            if (sampleBuffer != IntPtr.Zero && status == 0)
            {
                AppendAnnexB(sampleBuffer, frameContext.Accumulator);
            }
        }
        catch
        {
        }
        finally
        {
            frameContext.SignalCompletion();
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

        var keys = SharedConstants.Value;
        SetSessionCFProperty(keys.RealTime, keys.CFBooleanTrue);
        SetSessionCFProperty(keys.AllowFrameReordering, keys.CFBooleanFalse);
        SetSessionCFProperty(keys.ProfileLevel, keys.ProfileLevelH264BaselineAutoLevel);
        SetSessionInt32Property(keys.AverageBitRate, 2_500_000);
        SetSessionInt32Property(keys.ExpectedFrameRate, _timebaseFps);
        SetSessionInt32Property(keys.MaxKeyFrameInterval, Math.Max(1, _timebaseFps * 2));

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
        var keys = SharedConstants.Value;
        var dictKeys = new[] { keys.EncoderSpecEnableHardware };
        var dictValues = new[] { keys.CFBooleanTrue };
        return CFDictionaryCreate(
            IntPtr.Zero,
            dictKeys,
            dictValues,
            1,
            keys.DictionaryKeyCallbacks,
            keys.DictionaryValueCallbacks);
    }

    private static IntPtr CreateForceKeyFrameFrameProperties()
    {
        var keys = SharedConstants.Value;
        var dictKeys = new[] { keys.ForceKeyFrameFrameOption };
        var dictValues = new[] { keys.CFBooleanTrue };
        return CFDictionaryCreate(
            IntPtr.Zero,
            dictKeys,
            dictValues,
            1,
            keys.DictionaryKeyCallbacks,
            keys.DictionaryValueCallbacks);
    }

    private void SetSessionCFProperty(IntPtr key, IntPtr cfValue)
    {
        var status = VTSessionSetProperty(_compressionSession, key, cfValue);
        if (status != 0)
        {
            throw new InvalidOperationException($"VTSessionSetProperty failed (key=0x{key:X}): status={status}.");
        }
    }

    private void SetSessionInt32Property(IntPtr key, int value)
    {
        var cfNum = CreateCfInt32(value);
        if (cfNum == IntPtr.Zero)
        {
            throw new InvalidOperationException($"CFNumberCreate(Int32={value}) returned NULL.");
        }

        try
        {
            var status = VTSessionSetProperty(_compressionSession, key, cfNum);
            if (status != 0)
            {
                throw new InvalidOperationException($"VTSessionSetProperty failed (key=0x{key:X}, value={value}): status={status}.");
            }
        }
        finally
        {
            CFRelease(cfNum);
        }
    }

    private static IntPtr CreateCfInt32(int value)
    {
        var p = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            Marshal.WriteInt32(p, value);
            return CFNumberCreate(IntPtr.Zero, CFNumberType.kCFNumberSInt32Type, p);
        }
        finally
        {
            Marshal.FreeHGlobal(p);
        }
    }

    private static void AppendAnnexB(IntPtr sampleBuffer, EncodedFrameAccumulator acc)
    {
        if (IsKeyFrameSample(sampleBuffer))
        {
            AppendParameterSets(sampleBuffer, acc.Stream);
        }

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

        var avcc = new byte[totalLen];
        var tmpHandle = GCHandle.Alloc(avcc, GCHandleType.Pinned);
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

        WriteAvccAsAnnexB(avcc, acc.Stream);
    }

    private static bool IsKeyFrameSample(IntPtr sampleBuffer)
    {
        var attachmentsArray = CMSampleBufferGetSampleAttachmentsArray(sampleBuffer, createIfNecessary: false);
        if (attachmentsArray == IntPtr.Zero || CFArrayGetCount(attachmentsArray) <= 0)
        {
            return true;
        }

        var dict = CFArrayGetValueAtIndex(attachmentsArray, 0);
        if (dict == IntPtr.Zero)
        {
            return true;
        }

        var keys = SharedConstants.Value;
        var notSync = CFDictionaryGetValue(dict, keys.CMSampleAttachmentKeyNotSync);
        if (notSync == IntPtr.Zero)
        {
            return true;
        }

        return notSync != keys.CFBooleanTrue;
    }

    private static void AppendParameterSets(IntPtr sampleBuffer, MemoryStream output)
    {
        var formatDesc = CMSampleBufferGetFormatDescription(sampleBuffer);
        if (formatDesc == IntPtr.Zero)
        {
            return;
        }

        var probe = CMVideoFormatDescriptionGetH264ParameterSetAtIndex(
            formatDesc, 0, out _, out _, out var paramSetCount, out _);
        if (probe != 0)
        {
            return;
        }

        for (nuint i = 0; i < paramSetCount; i++)
        {
            var psStatus = CMVideoFormatDescriptionGetH264ParameterSetAtIndex(
                formatDesc, i, out var psPtr, out var psSize, out _, out _);
            if (psStatus != 0 || psPtr == IntPtr.Zero || psSize == 0)
            {
                continue;
            }

            output.Write(AnnexBStartCode, 0, AnnexBStartCode.Length);
            var psBytes = new byte[checked((int)psSize)];
            Marshal.Copy(psPtr, psBytes, 0, psBytes.Length);
            output.Write(psBytes, 0, psBytes.Length);
        }
    }

    private static void WriteAvccAsAnnexB(ReadOnlySpan<byte> avcc, MemoryStream dst)
    {
        var idx = 0;
        while (idx + 4 <= avcc.Length)
        {
            var nalLen = (int)BinaryPrimitives.ReadUInt32BigEndian(avcc.Slice(idx, 4));
            idx += 4;
            if (nalLen <= 0 || idx + nalLen > avcc.Length)
            {
                return;
            }

            dst.Write(AnnexBStartCode, 0, AnnexBStartCode.Length);
            dst.Write(avcc.Slice(idx, nalLen));
            idx += nalLen;
        }
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
    private const uint kCMVideoCodecType_H264 = 0x61766331;   // 'avc1'

    #region Core Foundation / VideoToolbox constants resolved via dlsym

    private sealed class VideoToolboxConstants
    {
        public IntPtr CFBooleanTrue { get; init; }
        public IntPtr CFBooleanFalse { get; init; }
        public IntPtr DictionaryKeyCallbacks { get; init; }
        public IntPtr DictionaryValueCallbacks { get; init; }

        public IntPtr RealTime { get; init; }
        public IntPtr AllowFrameReordering { get; init; }
        public IntPtr ProfileLevel { get; init; }
        public IntPtr ProfileLevelH264BaselineAutoLevel { get; init; }
        public IntPtr AverageBitRate { get; init; }
        public IntPtr ExpectedFrameRate { get; init; }
        public IntPtr MaxKeyFrameInterval { get; init; }
        public IntPtr ForceKeyFrameFrameOption { get; init; }
        public IntPtr EncoderSpecEnableHardware { get; init; }

        public IntPtr CMSampleAttachmentKeyNotSync { get; init; }

        public static VideoToolboxConstants Resolve()
        {
            var cf = Dlopen("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation");
            var cm = Dlopen("/System/Library/Frameworks/CoreMedia.framework/CoreMedia");
            var vt = Dlopen("/System/Library/Frameworks/VideoToolbox.framework/VideoToolbox");

            return new VideoToolboxConstants
            {
                CFBooleanTrue = DereferenceSymbol(cf, "kCFBooleanTrue"),
                CFBooleanFalse = DereferenceSymbol(cf, "kCFBooleanFalse"),
                DictionaryKeyCallbacks = Dlsym(cf, "kCFTypeDictionaryKeyCallBacks"),
                DictionaryValueCallbacks = Dlsym(cf, "kCFTypeDictionaryValueCallBacks"),

                RealTime = DereferenceSymbol(vt, "kVTCompressionPropertyKey_RealTime"),
                AllowFrameReordering = DereferenceSymbol(vt, "kVTCompressionPropertyKey_AllowFrameReordering"),
                ProfileLevel = DereferenceSymbol(vt, "kVTCompressionPropertyKey_ProfileLevel"),
                ProfileLevelH264BaselineAutoLevel = DereferenceSymbol(vt, "kVTProfileLevel_H264_Baseline_AutoLevel"),
                AverageBitRate = DereferenceSymbol(vt, "kVTCompressionPropertyKey_AverageBitRate"),
                ExpectedFrameRate = DereferenceSymbol(vt, "kVTCompressionPropertyKey_ExpectedFrameRate"),
                MaxKeyFrameInterval = DereferenceSymbol(vt, "kVTCompressionPropertyKey_MaxKeyFrameInterval"),
                ForceKeyFrameFrameOption = DereferenceSymbol(vt, "kVTEncodeFrameOptionKey_ForceKeyFrame"),
                EncoderSpecEnableHardware = DereferenceSymbol(vt, "kVTVideoEncoderSpecification_EnableHardwareAcceleratedVideoEncoder"),

                CMSampleAttachmentKeyNotSync = DereferenceSymbol(cm, "kCMSampleAttachmentKey_NotSync"),
            };
        }

        private static IntPtr Dlopen(string path)
        {
            const int RTLD_NOW = 2;
            var handle = dlopen(path, RTLD_NOW);
            if (handle == IntPtr.Zero)
            {
                throw new InvalidOperationException($"dlopen('{path}') failed.");
            }

            return handle;
        }

        private static IntPtr Dlsym(IntPtr handle, string symbol)
        {
            var addr = dlsym(handle, symbol);
            if (addr == IntPtr.Zero)
            {
                throw new InvalidOperationException($"dlsym('{symbol}') not found.");
            }

            return addr;
        }

        private static IntPtr DereferenceSymbol(IntPtr handle, string symbol)
            => Marshal.ReadIntPtr(Dlsym(handle, symbol));
    }

    #endregion

    #region P/Invoke

    [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "dlopen")]
    private static extern IntPtr dlopen([MarshalAs(UnmanagedType.LPStr)] string path, int mode);

    [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "dlsym")]
    private static extern IntPtr dlsym(IntPtr handle, [MarshalAs(UnmanagedType.LPStr)] string symbol);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFNumberCreate(IntPtr allocator, CFNumberType theType, IntPtr valuePtr);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFDictionaryCreate(IntPtr allocator, IntPtr[] keys, IntPtr[] values, nint numValues, IntPtr keyCallBacks, IntPtr valueCallBacks);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFDictionaryGetValue(IntPtr theDict, IntPtr key);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern nint CFArrayGetCount(IntPtr theArray);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFArrayGetValueAtIndex(IntPtr theArray, nint index);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);

    [DllImport("/System/Library/Frameworks/CoreMedia.framework/CoreMedia")]
    private static extern CMTime CMTimeMake(long value, int timescale);

    [DllImport("/System/Library/Frameworks/CoreMedia.framework/CoreMedia")]
    private static extern IntPtr CMSampleBufferGetDataBuffer(IntPtr sbuf);

    [DllImport("/System/Library/Frameworks/CoreMedia.framework/CoreMedia")]
    private static extern IntPtr CMSampleBufferGetFormatDescription(IntPtr sbuf);

    [DllImport("/System/Library/Frameworks/CoreMedia.framework/CoreMedia")]
    private static extern IntPtr CMSampleBufferGetSampleAttachmentsArray(IntPtr sbuf, [MarshalAs(UnmanagedType.I1)] bool createIfNecessary);

    [DllImport("/System/Library/Frameworks/CoreMedia.framework/CoreMedia")]
    private static extern int CMBlockBufferGetDataLength(IntPtr blockBuffer);

    [DllImport("/System/Library/Frameworks/CoreMedia.framework/CoreMedia")]
    private static extern int CMBlockBufferCopyDataBytes(IntPtr blockBuffer, nuint offset, nuint length, IntPtr dataPointer);

    [DllImport("/System/Library/Frameworks/CoreMedia.framework/CoreMedia")]
    private static extern int CMVideoFormatDescriptionGetH264ParameterSetAtIndex(
        IntPtr videoDesc,
        nuint parameterSetIndex,
        out IntPtr parameterSetPointerOut,
        out nuint parameterSetSizeOut,
        out nuint parameterSetCountOut,
        out int nalUnitHeaderLengthOut);

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
        IntPtr imageBuffer,
        CMTime presentationTimeStamp,
        CMTime duration,
        IntPtr frameProperties,
        IntPtr sourceFrameRefCon,
        out VTEncodeInfoFlags infoFlagsOut);

    [DllImport("/System/Library/Frameworks/VideoToolbox.framework/VideoToolbox")]
    private static extern int VTCompressionSessionCompleteFrames(
        IntPtr session,
        CMTime completeUntilPresentationTimeStamp);

    [DllImport("/System/Library/Frameworks/VideoToolbox.framework/VideoToolbox")]
    private static extern void VTCompressionSessionInvalidate(IntPtr session);

    [DllImport("/System/Library/Frameworks/VideoToolbox.framework/VideoToolbox")]
    private static extern void VTCompressionSessionRelease(IntPtr session);

    [DllImport("/System/Library/Frameworks/VideoToolbox.framework/VideoToolbox")]
    private static extern int VTSessionSetProperty(IntPtr session, IntPtr propertyKey, IntPtr propertyValue);

    #endregion

    private sealed class FrameEncodeContext : IDisposable
    {
        private readonly ManualResetEventSlim _completed = new();

        public EncodedFrameAccumulator Accumulator { get; } = new();

        public int Status { get; set; }

        public void SignalCompletion() => _completed.Set();

        public bool WaitForCompletion(TimeSpan timeout) => _completed.Wait(timeout);

        public byte[] ToArray() => Accumulator.ToArray();

        public void Dispose()
        {
            _completed.Dispose();
            Accumulator.Dispose();
        }
    }
}
