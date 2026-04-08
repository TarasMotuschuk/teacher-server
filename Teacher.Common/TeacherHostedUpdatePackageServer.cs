using System.Net;
using System.Net.Sockets;

namespace Teacher.Common;

public sealed class TeacherHostedUpdatePackageServer : IDisposable
{
    private readonly object _sync = new();
    private readonly HttpClient _httpClient = new();
    private readonly string _rootDirectory;
    private readonly int _port;
    private HttpListener? _listener;
    private Task? _listenerTask;

    public TeacherHostedUpdatePackageServer(string rootDirectory, int port = 5199)
    {
        _rootDirectory = rootDirectory;
        _port = port;
    }

    public async Task<HostedUpdatePackage> PreparePackageAsync(
        string version,
        string packageUrl,
        string? expectedSha256,
        string agentAddress,
        CancellationToken cancellationToken = default)
    {
        EnsureStarted();
        Directory.CreateDirectory(_rootDirectory);

        var versionDirectory = Path.Combine(_rootDirectory, version);
        Directory.CreateDirectory(versionDirectory);
        var localZipPath = Path.Combine(versionDirectory, "student-agent-update.zip");

        if (!File.Exists(localZipPath))
        {
            await using var source = await _httpClient.GetStreamAsync(packageUrl, cancellationToken);
            await using var destination = File.Create(localZipPath);
            await source.CopyToAsync(destination, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            ValidateSha256(localZipPath, expectedSha256);
        }

        return new HostedUpdatePackage(
            version,
            BuildPackageUrlForAgent(agentAddress, version),
            expectedSha256,
            localZipPath);
    }

    public async Task<HostedUpdatePackage> PrepareLocalPackageAsync(
        string version,
        string localZipPath,
        string? expectedSha256,
        string agentAddress,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureStarted();
        Directory.CreateDirectory(_rootDirectory);

        var versionDirectory = Path.Combine(_rootDirectory, version);
        Directory.CreateDirectory(versionDirectory);
        var cachedZipPath = Path.Combine(versionDirectory, "student-agent-update.zip");

        if (!string.Equals(Path.GetFullPath(localZipPath), Path.GetFullPath(cachedZipPath), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(localZipPath, cachedZipPath, overwrite: true);
        }

        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            ValidateSha256(cachedZipPath, expectedSha256);
        }

        return new HostedUpdatePackage(
            version,
            BuildPackageUrlForAgent(agentAddress, version),
            expectedSha256,
            cachedZipPath);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_listener is null)
            {
                return;
            }

            _listener.Stop();
            _listener.Close();
            _listener = null;
        }
    }

    private void EnsureStarted()
    {
        lock (_sync)
        {
            if (_listener is not null)
            {
                return;
            }

            var listener = new HttpListener();
            listener.Prefixes.Add($"http://*:{_port}/");
            listener.Start();
            _listener = listener;
            _listenerTask = Task.Run(ListenLoopAsync);
        }
    }

    private async Task ListenLoopAsync()
    {
        var listener = _listener;
        if (listener is null)
        {
            return;
        }

        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch
            {
                break;
            }

            _ = Task.Run(() => HandleRequestAsync(context));
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                context.Response.Close();
                return;
            }

            var path = context.Request.Url?.AbsolutePath.Trim('/') ?? string.Empty;
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length != 3 ||
                !string.Equals(segments[0], "updates", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(segments[2], "student-agent-update.zip", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.Close();
                return;
            }

            var filePath = Path.Combine(_rootDirectory, segments[1], "student-agent-update.zip");
            if (!File.Exists(filePath))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.Close();
                return;
            }

            context.Response.ContentType = "application/zip";
            await using var source = File.OpenRead(filePath);
            context.Response.ContentLength64 = source.Length;
            await source.CopyToAsync(context.Response.OutputStream);
            context.Response.OutputStream.Close();
        }
        catch
        {
            try
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.Close();
            }
            catch
            {
            }
        }
    }

    private static void ValidateSha256(string packagePath, string expectedSha256)
    {
        using var stream = File.OpenRead(packagePath);
        var hash = System.Security.Cryptography.SHA256.HashData(stream);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();
        if (!string.Equals(actual, expectedSha256.Trim().ToLowerInvariant(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Cached update package checksum does not match the manifest.");
        }
    }

    private string BuildPackageUrlForAgent(string agentAddress, string version)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Connect(agentAddress, 9);
        var localIp = ((IPEndPoint)socket.LocalEndPoint!).Address;
        return $"http://{localIp}:{_port}/updates/{version}/student-agent-update.zip";
    }
}
