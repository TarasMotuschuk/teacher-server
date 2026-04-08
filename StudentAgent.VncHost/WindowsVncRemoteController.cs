using RemoteViewing.Vnc;
using RemoteViewing.Vnc.Server;

namespace StudentAgent.VncHost;

internal sealed class WindowsVncRemoteController : IVncRemoteController
{
    private readonly VncMouse _mouse = new();

    public void HandleTouchEvent(object? sender, PointerChangedEventArgs e)
    {
        _mouse.OnMouseUpdate(sender, e);
    }
}
