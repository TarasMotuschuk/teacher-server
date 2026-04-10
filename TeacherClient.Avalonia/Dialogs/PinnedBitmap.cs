using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;

namespace TeacherClient.CrossPlatform.Dialogs;

internal sealed class PinnedBitmap(Bitmap bitmap, GCHandle handle) : IDisposable
{
    public Bitmap Bitmap { get; } = bitmap;

    private GCHandle Handle { get; } = handle;

    public void Dispose()
    {
        Bitmap.Dispose();
        if (Handle.IsAllocated)
        {
            Handle.Free();
        }
    }
}
