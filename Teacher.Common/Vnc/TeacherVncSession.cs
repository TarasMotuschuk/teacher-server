using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.InteropServices;
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
            InitialRenderTarget = _renderTarget
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
                connection.CloseAsync().GetAwaiter().GetResult();
            }
            catch
            {
            }

            connection.Dispose();
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

                Monitor.Enter(_sync);
                return new FramebufferReference(this);
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

                var pixels = new byte[_buffer.Length];
                Buffer.BlockCopy(_buffer, 0, pixels, 0, _buffer.Length);

                // MarcusW.VncClient renders PixelFormat.Plain (RGBA). The existing viewers expect BGRA.
                for (var i = 0; i + 3 < pixels.Length; i += 4)
                {
                    (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
                }

                return new VncFrameCapture(_width, _height, _width * 4, pixels);
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                if (_bufferHandle.IsAllocated)
                {
                    _bufferHandle.Free();
                }

                _buffer = [];
                _width = 0;
                _height = 0;
            }
        }

        private sealed class FramebufferReference(FramebufferRenderTarget owner) : IFramebufferReference
        {
            private bool _disposed;

            public IntPtr Address
            {
                get
                {
                    lock (owner._sync)
                    {
                        return owner._bufferHandle.AddrOfPinnedObject();
                    }
                }
            }

            public Size Size
            {
                get
                {
                    lock (owner._sync)
                    {
                        return new Size(owner._width, owner._height);
                    }
                }
            }

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
                owner.FrameUpdated?.Invoke(owner, EventArgs.Empty);
                Monitor.Exit(owner._sync);
            }
        }
    }
}
