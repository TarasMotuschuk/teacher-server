using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TeacherClient.CrossPlatform;

public sealed class PinnedPreviewBitmap : IDisposable
{
    private readonly GCHandle _handle;

    private PinnedPreviewBitmap(Bitmap bitmap, GCHandle handle)
    {
        Bitmap = bitmap;
        _handle = handle;
    }

    public Bitmap Bitmap { get; }

    public static PinnedPreviewBitmap Create(byte[] pixels, int width, int height, int stride)
    {
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            var bitmap = new Bitmap(
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque,
                handle.AddrOfPinnedObject(),
                new PixelSize(width, height),
                new Vector(96, 96),
                stride);

            return new PinnedPreviewBitmap(bitmap, handle);
        }
        catch
        {
            handle.Free();
            throw;
        }
    }

    public void Dispose()
    {
        Bitmap.Dispose();
        if (_handle.IsAllocated)
        {
            _handle.Free();
        }
    }
}
