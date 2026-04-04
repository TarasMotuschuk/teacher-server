using System.Drawing.Imaging;
using System.Net;
using System.Runtime.InteropServices;
using RemoteViewing.Vnc;

namespace Teacher.Common.Vnc;

public sealed class TeacherVncSession : IAsyncDisposable, IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _sharedSecret;
    private readonly SemaphoreSlim _captureGate = new(1, 1);
    private readonly object _sync = new();
    private VncClient? _client;
    private bool _disposed;

    public TeacherVncSession(string host, int port, string sharedSecret, bool controlEnabled = false)
    {
        _host = host;
        _port = port;
        _sharedSecret = sharedSecret;
        ControlEnabled = controlEnabled;
    }

    public bool ControlEnabled { get; set; }

    public bool IsConnected => _client?.IsConnected ?? false;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler? Connected;
    public event EventHandler? Closed;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsConnected)
        {
            return;
        }

        var client = new VncClient();
        client.MaxUpdateRate = 10;
        client.ConnectionFailed += ClientOnConnectionFailed;
        client.Connected += ClientOnConnected;
        client.Closed += ClientOnClosed;

        var connectOptions = new VncClientConnectOptions
        {
            Password = VncPasswordHelper.Derive(_sharedSecret).ToCharArray(),
            OnDemandMode = false,
            ShareDesktop = true
        };

        try
        {
            await Task.Run(() => client.Connect(_host, _port, connectOptions), cancellationToken);
            lock (_sync)
            {
                _client = client;
            }

            await CaptureFrameAsync(cancellationToken);
            StatusChanged?.Invoke(this, "Connected");
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async Task<VncFrameCapture?> CaptureFrameAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return null;
        }

        var client = _client;
        if (client is null || !client.IsConnected || client.Framebuffer is null)
        {
            return null;
        }

        try
        {
            await _captureGate.WaitAsync(cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            return null;
        }

        try
        {
            if (_disposed)
            {
                return null;
            }

            client = _client;
            if (client is null || !client.IsConnected || client.Framebuffer is null)
            {
                return null;
            }

            var framebuffer = client.Framebuffer;
            var width = Math.Max(1, framebuffer.Width);
            var height = Math.Max(1, framebuffer.Height);
            var stride = width * 4;
            var pixels = new byte[stride * height];
            Action<IntPtr, int>? copier;
            try
            {
                copier = client.GetFramebuffer(null, null, null, null);
            }
            catch (ObjectDisposedException)
            {
                return null;
            }

            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                copier(handle.AddrOfPinnedObject(), stride);
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
            finally
            {
                handle.Free();
            }

            return new VncFrameCapture(width, height, stride, pixels);
        }
        finally
        {
            _captureGate.Release();
        }
    }

    public void SendPointer(int x, int y, int pressedButtons)
    {
        var client = _client;
        if (client?.IsConnected == true && ControlEnabled)
        {
            client.SendPointerEvent(x, y, pressedButtons);
        }
    }

    public void SendKey(KeySym keySym, bool pressed)
    {
        var client = _client;
        if (client?.IsConnected == true && ControlEnabled)
        {
            client.SendKeyEvent(keySym, pressed);
        }
    }

    public void Close()
    {
        _disposed = true;
        var client = Interlocked.Exchange(ref _client, null);
        if (client is null)
        {
            return;
        }

        client.ConnectionFailed -= ClientOnConnectionFailed;
        client.Connected -= ClientOnConnected;
        client.Closed -= ClientOnClosed;
        client.Close();
        client.Dispose();
    }

    private void ClientOnConnectionFailed(object? sender, EventArgs e)
    {
        StatusChanged?.Invoke(this, "Connection failed");
    }

    private void ClientOnConnected(object? sender, EventArgs e)
    {
        Connected?.Invoke(this, EventArgs.Empty);
    }

    private void ClientOnClosed(object? sender, EventArgs e)
    {
        Closed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Close();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
