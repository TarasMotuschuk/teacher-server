using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Teacher.Common;
using Teacher.Common.Contracts;

namespace TeacherClient.Services;

public sealed class TeacherApiClient
{
    public sealed record TransferProgress(long BytesTransferred, long? TotalBytes)
    {
        public bool HasTotal => TotalBytes.HasValue && TotalBytes.Value > 0;
        public double ProgressRatio => HasTotal ? (double)BytesTransferred / TotalBytes!.Value : 0d;
        public int Percent => HasTotal ? (int)Math.Clamp(Math.Round(ProgressRatio * 100d), 0, 100) : 0;
    }

    private readonly HttpClient _httpClient;

    public TeacherApiClient(string baseAddress, string sharedSecret)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(AppendTrailingSlash(baseAddress))
        };
        _httpClient.DefaultRequestHeaders.Add("X-Teacher-Secret", sharedSecret);
    }

    public Task<ServerInfoDto?> GetServerInfoAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<ServerInfoDto>("api/info", cancellationToken);

    public async Task<bool> IsServerReachableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<ProcessInfoDto>> GetProcessesAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<ProcessInfoDto>>("api/processes", cancellationToken) ?? [];

    public async Task KillProcessAsync(int processId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/processes/kill", new KillProcessRequest(processId), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<ProcessDetailsDto?> GetProcessDetailsAsync(int processId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<ProcessDetailsDto>($"api/processes/{processId}", cancellationToken);

    public async Task RestartProcessAsync(int processId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/processes/restart", new RestartProcessRequest(processId), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetBrowserLockEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/browser-lock", new BrowserLockStateRequest(enabled), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetInputLockEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/input-lock", new InputLockStateRequest(enabled), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ApplyStudentPolicySettingsAsync(int desktopIconAutoRestoreMinutes, int browserLockCheckIntervalSeconds, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/policy-settings",
            new StudentPolicySettingsRequest(desktopIconAutoRestoreMinutes, browserLockCheckIntervalSeconds),
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ExecutePowerActionAsync(PowerActionKind action, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/power", new PowerActionRequest(action), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<RegistryKeyDto>> GetRegistrySubKeysAsync(string path, CancellationToken cancellationToken = default)
    {
        var requestUri = string.IsNullOrEmpty(path)
            ? "api/registry/keys"
            : $"api/registry/keys?path={Uri.EscapeDataString(path)}";
        return await _httpClient.GetFromJsonAsync<List<RegistryKeyDto>>(requestUri, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<RegistryValueDto>> GetRegistryValuesAsync(string path, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<RegistryValueDto>>(
            $"api/registry/values?path={Uri.EscapeDataString(path)}", cancellationToken) ?? [];

    public async Task<IReadOnlyList<RegistryValueEditDto>> GetRegistryValuesForEditAsync(string path, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<RegistryValueEditDto>>(
            $"api/registry/values/edit?path={Uri.EscapeDataString(path)}", cancellationToken) ?? [];

    public async Task SetRegistryValueAsync(string path, string name, string type, string data, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/registry/values", new SetRegistryValueRequest(path, name, type, data), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteRegistryValueAsync(string path, string name, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "api/registry/values")
        {
            Content = JsonContent.Create(new DeleteRegistryValueRequest(path, name))
        };
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CreateRegistryKeyAsync(string parentPath, string keyName, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/registry/keys", new CreateRegistryKeyRequest(parentPath, keyName), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteRegistryKeyAsync(string path, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "api/registry/keys")
        {
            Content = JsonContent.Create(new DeleteRegistryKeyRequest(path))
        };
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ExportRegistryKeyAsync(string path, string destinationFilePath, CancellationToken cancellationToken = default)
    {
        await using var source = await _httpClient.GetStreamAsync(
            $"api/registry/export?path={Uri.EscapeDataString(path)}",
            cancellationToken);
        await using var destination = File.Create(destinationFilePath);
        await source.CopyToAsync(destination, cancellationToken);
    }

    public async Task<ImportRegistryFileResult?> ImportRegistryFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        using var response = await _httpClient.PostAsync("api/registry/import", content, cancellationToken);
        await EnsureSuccessWithServerErrorAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ImportRegistryFileResult>(cancellationToken);
    }

    public Task<AgentUpdateStatusDto?> GetUpdateStatusAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<AgentUpdateStatusDto>("api/update/status", cancellationToken);

    public Task<UpdateInfoDto?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<UpdateInfoDto>("api/update/check", cancellationToken);

    public async Task<AgentUpdateStatusDto?> StartAgentUpdateAsync(bool checkForUpdatesFirst = true, CancellationToken cancellationToken = default)
        => await StartAgentUpdateAsync(new StartAgentUpdateRequest(checkForUpdatesFirst), cancellationToken);

    public async Task<AgentUpdateStatusDto?> StartAgentUpdateAsync(StartAgentUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/update/start", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentUpdateStatusDto>(cancellationToken);
    }

    public Task<VncStateDto?> GetVncStatusAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<VncStateDto>("api/vnc/status", cancellationToken);

    public async Task StartVncAsync(bool viewOnly = true, int? port = null, string? password = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/vnc/start", new StartVncRequest(port, viewOnly, password), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task StopVncAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/vnc/stop", new StopVncRequest(), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<string>> GetRootsAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<string>>("api/files/roots", cancellationToken) ?? [];

    public Task<DriveSpaceDto?> GetRemoteDriveSpaceAsync(string? path, CancellationToken cancellationToken = default)
    {
        var requestUri = string.IsNullOrWhiteSpace(path)
            ? "api/files/space"
            : $"api/files/space?path={Uri.EscapeDataString(path)}";

        return _httpClient.GetFromJsonAsync<DriveSpaceDto>(requestUri, cancellationToken);
    }

    public async Task<DirectoryListingDto?> GetRemoteDirectoryAsync(string? path, CancellationToken cancellationToken = default)
    {
        var requestUri = string.IsNullOrWhiteSpace(path)
            ? "api/files/list"
            : $"api/files/list?path={Uri.EscapeDataString(path)}";

        return await _httpClient.GetFromJsonAsync<DirectoryListingDto>(requestUri, cancellationToken);
    }

    public async Task DeleteRemoteEntryAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "api/files")
        {
            Content = JsonContent.Create(new DeleteEntryRequest(fullPath))
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CreateRemoteDirectoryAsync(string parentPath, string name, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/files/directories", new CreateDirectoryRequest(parentPath, name), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RenameRemoteEntryAsync(string fullPath, string newName, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/files/rename", new RenameEntryRequest(fullPath, newName), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ClearRemoteDirectoryAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/files/clear-directory", new ClearDirectoryRequest(fullPath), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task EnsureSharedWritableDirectoryAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/files/shared-directory", new EnsureSharedDirectoryRequest(fullPath), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task OpenRemoteEntryAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/files/open", new OpenRemoteEntryRequest(fullPath), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ExecuteRemoteCommandAsync(string script, RemoteCommandRunAs runAs, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/commands/run", new RemoteCommandRequest(script, runAs), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<FrequentProgramShortcutDto>> GetPublicDesktopShortcutsAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<FrequentProgramShortcutDto>>("api/commands/frequent-programs/public-desktop", cancellationToken) ?? [];

    public async Task<IReadOnlyList<DesktopIconLayoutSummaryDto>> GetDesktopIconLayoutsAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<DesktopIconLayoutSummaryDto>>("api/desktop-icons/layouts", cancellationToken) ?? [];

    public Task<DesktopIconLayoutSnapshotDto?> GetDesktopIconLayoutAsync(string? layoutName = null, CancellationToken cancellationToken = default)
    {
        var requestUri = string.IsNullOrWhiteSpace(layoutName)
            ? "api/desktop-icons/layout"
            : $"api/desktop-icons/layout?name={Uri.EscapeDataString(layoutName)}";

        return _httpClient.GetFromJsonAsync<DesktopIconLayoutSnapshotDto>(requestUri, cancellationToken);
    }

    public async Task<DesktopIconLayoutOperationResultDto?> SaveDesktopIconLayoutAsync(string? layoutName = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/desktop-icons/save", new SaveDesktopIconLayoutRequest(layoutName), cancellationToken);
        await EnsureSuccessWithServerErrorAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<DesktopIconLayoutOperationResultDto>(cancellationToken);
    }

    public async Task<DesktopIconLayoutOperationResultDto?> RestoreDesktopIconLayoutAsync(string? layoutName = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/desktop-icons/restore", new RestoreDesktopIconLayoutRequest(layoutName), cancellationToken);
        await EnsureSuccessWithServerErrorAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<DesktopIconLayoutOperationResultDto>(cancellationToken);
    }

    public async Task<DesktopIconLayoutOperationResultDto?> ApplyDesktopIconLayoutAsync(
        DesktopIconLayoutSnapshotDto layout,
        string? targetLayoutName = null,
        bool restoreAfterApply = true,
        CancellationToken cancellationToken = default)
    {
        var request = new ApplyDesktopIconLayoutRequest(layout, targetLayoutName, restoreAfterApply);
        var response = await _httpClient.PostAsJsonAsync("api/desktop-icons/apply", request, cancellationToken);
        await EnsureSuccessWithServerErrorAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<DesktopIconLayoutOperationResultDto>(cancellationToken);
    }

    public async Task EnsureRemoteDirectoryPathAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        var normalizedPath = RemoteWindowsPath.Normalize(fullPath);
        if (string.IsNullOrWhiteSpace(normalizedPath) || RemoteWindowsPath.IsDriveRoot(normalizedPath))
        {
            return;
        }

        if (!RemoteWindowsPath.TryGetParentAndName(normalizedPath, out var parentPath, out var directoryName))
        {
            return;
        }

        await CreateRemoteDirectoryAsync(parentPath, directoryName, cancellationToken);
    }

    public async Task DownloadRemoteFileAsync(
        string remotePath,
        string localDirectory,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var fileName = RemoteWindowsPath.GetFileName(remotePath);
        var destinationPath = Path.Combine(localDirectory, fileName);
        using var response = await _httpClient.GetAsync(
            $"api/files/download?fullPath={Uri.EscapeDataString(remotePath)}",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var totalBytes = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = File.Create(destinationPath);
        await CopyStreamWithProgressAsync(source, destination, totalBytes, progress, cancellationToken);
    }

    public async Task UploadFileAsync(
        string localPath,
        string remoteDirectory,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(localPath);
        using var content = new MultipartFormDataContent();
        using var fileContent = new ProgressStreamContent(stream, progress);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", Path.GetFileName(localPath));
        content.Add(new StringContent(remoteDirectory), "destinationDirectory");

        using var response = await _httpClient.PostAsync("api/files/upload", content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static string AppendTrailingSlash(string baseAddress)
        => baseAddress.EndsWith("/", StringComparison.Ordinal) ? baseAddress : $"{baseAddress}/";

    private static async Task EnsureSuccessWithServerErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var errorText = await TryReadServerErrorAsync(response, cancellationToken);
        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(errorText)
                ? $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase})."
                : errorText,
            inner: null,
            response.StatusCode);
    }

    private static async Task<string?> TryReadServerErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("error", out var errorElement) &&
                errorElement.ValueKind == JsonValueKind.String)
            {
                return errorElement.GetString();
            }

            return payload;
        }
        catch
        {
            return null;
        }
    }

    private static async Task CopyStreamWithProgressAsync(
        Stream source,
        Stream destination,
        long? totalBytes,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long transferred = 0;
        progress?.Report(new TransferProgress(0, totalBytes));

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            transferred += read;
            progress?.Report(new TransferProgress(transferred, totalBytes));
        }
    }

    private sealed class ProgressStreamContent : HttpContent
    {
        private readonly Stream _source;
        private readonly IProgress<TransferProgress>? _progress;
        private readonly long? _length;

        public ProgressStreamContent(Stream source, IProgress<TransferProgress>? progress)
        {
            _source = source;
            _progress = progress;
            _length = source.CanSeek ? source.Length : null;
        }

        protected override bool TryComputeLength(out long length)
        {
            if (_length.HasValue)
            {
                length = _length.Value;
                return true;
            }

            length = 0;
            return false;
        }

        protected override async Task SerializeToStreamAsync(Stream targetStream, TransportContext? context)
        {
            var buffer = new byte[81920];
            long transferred = 0;
            _progress?.Report(new TransferProgress(0, _length));

            while (true)
            {
                var read = await _source.ReadAsync(buffer);
                if (read == 0)
                {
                    break;
                }

                await targetStream.WriteAsync(buffer.AsMemory(0, read));
                transferred += read;
                _progress?.Report(new TransferProgress(transferred, _length));
            }
        }
    }
}
