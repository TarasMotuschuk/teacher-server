using System.Net.Http.Headers;
using System.Net.Http.Json;
using Teacher.Common;
using Teacher.Common.Contracts;

namespace TeacherClient.Services;

public sealed class TeacherApiClient
{
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
        response.EnsureSuccessStatusCode();
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

    public async Task<IReadOnlyList<string>> GetRootsAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<string>>("api/files/roots", cancellationToken) ?? [];

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

    public async Task DownloadRemoteFileAsync(string remotePath, string localDirectory, CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetFileName(remotePath);
        var destinationPath = Path.Combine(localDirectory, fileName);
        await using var source = await _httpClient.GetStreamAsync($"api/files/download?fullPath={Uri.EscapeDataString(remotePath)}", cancellationToken);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken);
    }

    public async Task UploadFileAsync(string localPath, string remoteDirectory, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(localPath);
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", Path.GetFileName(localPath));
        content.Add(new StringContent(remoteDirectory), "destinationDirectory");

        using var response = await _httpClient.PostAsync("api/files/upload", content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static string AppendTrailingSlash(string baseAddress)
        => baseAddress.EndsWith("/", StringComparison.Ordinal) ? baseAddress : $"{baseAddress}/";
}
