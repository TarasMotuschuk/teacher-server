using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using MarcusW.VncClient;
using MarcusW.VncClient.Protocol.Implementation.MessageTypes.Outgoing;
using MarcusW.VncClient.Protocol.Implementation.Services.Transports;
using MarcusW.VncClient.Rendering;
using MarcusW.VncClient.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Teacher.Common.Vnc;

public sealed class TeacherVncSession : IAsyncDisposable, IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _sharedSecret;
    private readonly FramebufferRenderTarget _renderTarget = new();
    private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private RfbConnection? _connection;
    private bool _disposed;

    public TeacherVncSession(string host, int port, string sharedSecret, bool controlEnabled = false)
    {
        _host = host;
        _port = port;
        _sharedSecret = sharedSecret;
        ControlEnabled = controlEnabled;
        _renderTarget.FrameUpdated += (_, _) => StatusChanged?.Invoke(this, "Frame updated");
    }

    public bool ControlEnabled { get; set; }

    public bool IsConnected => _connection?.ConnectionState == ConnectionState.Connected;

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

        var client = new VncClient(_loggerFactory);
        var parameters = new ConnectParameters
        {
            TransportParameters = new TcpTransportParameters
            {
                Host = _host,
                Port = _port
            },
            AuthenticationHandler = new StaticAuthenticationHandler(VncPasswordHelper.Derive(_sharedSecret)),
            AllowSharedConnection = true,
            InitialRenderTarget = _renderTarget,
            // When the server uses Tight subencoding with JPEG, prefer high quality and full chroma (visually lossless for typical UI).
            // Lossless zlib/ZRLE rectangles ignore these; they are negotiated separately by the RFB stack.
            JpegQualityLevel = 95,
            JpegSubsamplingLevel = JpegSubsamplingLevel.None
        };

        var connection = await client.ConnectAsync(parameters, cancellationToken);
        connection.PropertyChanged += ConnectionOnPropertyChanged;
        _connection = connection;
        StatusChanged?.Invoke(this, "Connected");
        Connected?.Invoke(this, EventArgs.Empty);
    }

    public Task<VncFrameCapture?> CaptureFrameAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_renderTarget.Capture());
    }

    public void SendPointer(int x, int y, int pressedButtons)
    {
        var connection = _connection;
        if (connection is null || connection.ConnectionState != ConnectionState.Connected || !ControlEnabled)
        {
            return;
        }

        _ = connection.SendMessageAsync(
            new PointerEventMessage(new Position(x, y), ToMouseButtons(pressedButtons)),
            CancellationToken.None);
    }

    public void SendKey(KeySymbol keySymbol, bool pressed)
    {
        var connection = _connection;
        if (connection is null || connection.ConnectionState != ConnectionState.Connected || !ControlEnabled)
        {
            return;
        }

        _ = connection.SendMessageAsync(
            new KeyEventMessage(pressed, keySymbol),
            CancellationToken.None);
    }

    public void Close()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var connection = Interlocked.Exchange(ref _connection, null);
        if (connection is not null)
        {
            connection.PropertyChanged -= ConnectionOnPropertyChanged;
            try
            {
                // Blocking Wait on the UI thread + async continuations that post to the same sync context
                // causes deadlocks on Avalonia/macOS during shutdown. Clearing the synchronization context
                // for this wait avoids that without moving work to another thread (VNC close/dispose is not
                // thread-safe across threads in all library builds).
                var previous = SynchronizationContext.Current;
                try
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                    connection.CloseAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previous);
                }

                connection.Dispose();
            }
            catch
            {
            }
        }

        _renderTarget.Dispose();
    }

    private void ConnectionOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not RfbConnection connection)
        {
            return;
        }

        if (string.Equals(e.PropertyName, nameof(RfbConnection.ConnectionState), StringComparison.Ordinal))
        {
            switch (connection.ConnectionState)
            {
                case ConnectionState.Connected:
                    StatusChanged?.Invoke(this, "Connected");
                    break;
                case ConnectionState.Interrupted:
                case ConnectionState.ReconnectFailed:
                case ConnectionState.Reconnecting:
                    StatusChanged?.Invoke(this, connection.InterruptionCause?.Message ?? connection.ConnectionState.ToString());
                    break;
                case ConnectionState.Closed:
                    StatusChanged?.Invoke(this, connection.InterruptionCause?.Message ?? "Closed");
                    Closed?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
    }

    private static MouseButtons ToMouseButtons(int pressedButtons)
    {
        var buttons = MouseButtons.None;
        if ((pressedButtons & 1) != 0)
        {
            buttons |= MouseButtons.Left;
        }
        if ((pressedButtons & 2) != 0)
        {
            buttons |= MouseButtons.Middle;
        }
        if ((pressedButtons & 4) != 0)
        {
            buttons |= MouseButtons.Right;
        }
        if ((pressedButtons & 8) != 0)
        {
            buttons |= MouseButtons.WheelUp;
        }
        if ((pressedButtons & 16) != 0)
        {
            buttons |= MouseButtons.WheelDown;
        }

        return buttons;
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

    private sealed class StaticAuthenticationHandler(string password) : IAuthenticationHandler
    {
        public Task<TInput> ProvideAuthenticationInputAsync<TInput>(
            RfbConnection connection,
            MarcusW.VncClient.Protocol.SecurityTypes.ISecurityType securityType,
            IAuthenticationInputRequest<TInput> request)
            where TInput : class, IAuthenticationInput
        {
            if (typeof(TInput) == typeof(PasswordAuthenticationInput))
            {
                return Task.FromResult((TInput)(object)new PasswordAuthenticationInput(password));
            }

            throw new NotSupportedException($"Unsupported authentication request type: {typeof(TInput).Name}");
        }
    }

    private sealed class FramebufferRenderTarget : IRenderTarget, IDisposable
    {
        private readonly object _sync = new();
        private byte[] _buffer = [];
        private GCHandle _bufferHandle;
        private int _width;
        private int _height;
        private int _activeReferences;
        private bool _disposed;

        public event EventHandler? FrameUpdated;

        public IFramebufferReference GrabFramebufferReference(Size size, IImmutableSet<Screen> layout)
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                var width = Math.Max(1, size.Width);
                var height = Math.Max(1, size.Height);
                var requiredLength = width * height * 4;

                while ((_width != width || _height != height || _buffer.Length != requiredLength) && _activeReferences > 0)
                {
                    Monitor.Wait(_sync);
                    ObjectDisposedException.ThrowIf(_disposed, this);
                }

                if (_buffer.Length != requiredLength)
                {
                    if (_bufferHandle.IsAllocated)
                    {
                        _bufferHandle.Free();
                    }

                    _buffer = new byte[requiredLength];
                    _bufferHandle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
                    _width = width;
                    _height = height;
                }

                _activeReferences++;
                return new FramebufferReference(this, _bufferHandle.AddrOfPinnedObject(), new Size(_width, _height));
            }
        }

        public VncFrameCapture? Capture()
        {
            lock (_sync)
            {
                if (_disposed || _buffer.Length == 0 || _width <= 0 || _height <= 0)
                {
                    return null;
                }

                // MarcusW Plain is a 32-bit value R<<24|G<<16|B<<8|A; in little-endian memory that is bytes A,B,G,R (see Color.ToPlainPixel in MarcusW.VncClient).
                var raw = new byte[_buffer.Length];
                Buffer.BlockCopy(_buffer, 0, raw, 0, _buffer.Length);

                var bgra = new byte[raw.Length];
                for (var i = 0; i + 3 < bgra.Length; i += 4)
                {
                    bgra[i] = raw[i + 1];
                    bgra[i + 1] = raw[i + 2];
                    bgra[i + 2] = raw[i + 3];
                    bgra[i + 3] = 255;
                }

                return new VncFrameCapture(_width, _height, _width * 4, bgra);
            }
        }

        public void Dispose()
        {
            byte[]? bufferToClear = null;

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                while (_activeReferences > 0)
                {
                    Monitor.Wait(_sync);
                }

                if (_bufferHandle.IsAllocated)
                {
                    _bufferHandle.Free();
                }

                bufferToClear = _buffer;
                _buffer = [];
                _width = 0;
                _height = 0;
            }

            if (bufferToClear is not null)
            {
                Array.Clear(bufferToClear);
            }
        }

        private sealed class FramebufferReference(FramebufferRenderTarget owner, IntPtr address, Size size) : IFramebufferReference
        {
            private bool _disposed;

            public IntPtr Address { get; } = address;

            public Size Size { get; } = size;

            public PixelFormat Format => PixelFormat.Plain;

            public double HorizontalDpi => 96;

            public double VerticalDpi => 96;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                lock (owner._sync)
                {
                    owner._activeReferences = Math.Max(0, owner._activeReferences - 1);
                    Monitor.PulseAll(owner._sync);
                }

                owner.FrameUpdated?.Invoke(owner, EventArgs.Empty);
            }
        }
    }
}
