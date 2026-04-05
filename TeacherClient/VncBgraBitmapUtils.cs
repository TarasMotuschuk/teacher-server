#nullable enable

using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TeacherClient;

/// <summary>
/// VNC captures are tightly packed BGRA; GDI+ 32bpp bitmaps may use a larger per-row stride.
/// </summary>
internal static class VncBgraBitmapUtils
{
    public static void CopyTightBgraToLockedBitmap(byte[] pixels, int width, int height, int srcStride, BitmapData dest)
    {
        var rowBytes = width * 4;
        if (srcStride < rowBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(srcStride));
        }

        var destStride = Math.Abs(dest.Stride);
        var required = srcStride * height;
        if (pixels.Length < required)
        {
            throw new ArgumentException("Pixel buffer too small for dimensions.", nameof(pixels));
        }

        if (srcStride == rowBytes && destStride == rowBytes)
        {
            Marshal.Copy(pixels, 0, dest.Scan0, rowBytes * height);
            return;
        }

        for (var y = 0; y < height; y++)
        {
            Marshal.Copy(pixels, y * srcStride, IntPtr.Add(dest.Scan0, y * destStride), rowBytes);
        }
    }
}
