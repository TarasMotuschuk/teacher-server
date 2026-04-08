using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using RemoteViewing.Vnc;
using StudentAgent.Services;
using StudentAgent.UI.Localization;

namespace StudentAgent.VncHost;

internal sealed class DesktopCaptureFramebufferSource : IVncFramebufferSource
{
    private readonly object _sync = new();
    private readonly AgentLogService _logService;
    private VncFramebuffer? _framebuffer;
    private int _width;
    private int _height;

    public DesktopCaptureFramebufferSource(AgentLogService logService)
    {
        _logService = logService;
    }

    public bool SupportsResizing => false;

    public VncFramebuffer Capture()
    {
        lock (_sync)
        {
            var bounds = SystemInformation.VirtualScreen;
            var width = Math.Max(1, bounds.Width);
            var height = Math.Max(1, bounds.Height);

            if (_framebuffer is null || _width != width || _height != height)
            {
                _width = width;
                _height = height;
                _framebuffer = new VncFramebuffer(
                    StudentAgentText.AgentName,
                    width,
                    height,
                    VncPixelFormat.RGB32);
            }

            try
            {
                using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                InputDesktopGdiCapture.CopyVirtualScreenToBitmap(bounds, width, height, bitmap);

                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppRgb);

                try
                {
                    var buffer = _framebuffer.GetBuffer();
                    lock (_framebuffer.SyncRoot)
                    {
                        var sourceStride = Math.Abs(bitmapData.Stride);
                        var targetStride = _framebuffer.Stride;
                        var bytesPerRow = Math.Min(sourceStride, targetStride);

                        for (var row = 0; row < height; row++)
                        {
                            var sourceRow = IntPtr.Add(bitmapData.Scan0, row * bitmapData.Stride);
                            Marshal.Copy(sourceRow, buffer, row * targetStride, bytesPerRow);
                        }
                    }
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"VNC desktop capture failed: {ex.Message}");
            }

            return _framebuffer;
        }
    }

    public ExtendedDesktopSizeStatus SetDesktopSize(int width, int height)
        => ExtendedDesktopSizeStatus.Prohibited;
}
