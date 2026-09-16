using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Teacher.Common.Contracts.Testing;

namespace TeacherClient.CrossPlatform.Services;

public sealed class TestPlatformApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly HttpClient _http;

    public TestPlatformApiClient(string baseUrl)
    {
        var normalized = baseUrl.Trim().TrimEnd('/');
        _http = new HttpClient
        {
            BaseAddress = new Uri(normalized + "/"),
            Timeout = TimeSpan.FromSeconds(30),
        };
    }

    public async Task CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("health", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<PagedResponseDto<TestDefinitionListItemDto>> ListTestDefinitionsAsync(
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrWhiteSpace(search)
            ? "api/tests/v1/test-definitions"
            : $"api/tests/v1/test-definitions?search={Uri.EscapeDataString(search.Trim())}";
        using var response = await _http.GetAsync(path, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PagedResponseDto<TestDefinitionListItemDto>>(JsonOptions, cancellationToken)
            ?? new PagedResponseDto<TestDefinitionListItemDto>([], 0);
    }

    public async Task<MyTestImportResponseDto> ImportMyTestXmlAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        using var response = await _http.PostAsync("api/tests/v1/imports/mytest-xml", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<MyTestImportResponseDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Empty import response.");
    }

    public async Task<MyTestImportResponseDto> ImportCctestAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        using var response = await _http.PostAsync("api/tests/v1/imports/cctest", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<MyTestImportResponseDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Empty import response.");
    }

    public async Task<PagedResponseDto<AssignmentDto>> ListAssignmentsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("api/tests/v1/assignments", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PagedResponseDto<AssignmentDto>>(JsonOptions, cancellationToken)
            ?? new PagedResponseDto<AssignmentDto>([], 0);
    }

    public async Task<AssignmentDto> CreateAssignmentAsync(
        CreateAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("api/tests/v1/assignments", request, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AssignmentDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Empty create-assignment response.");
    }

    public async Task<AssignmentDto> CloseAssignmentAsync(
        string assignmentPublicId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsync(
            $"api/tests/v1/assignments/{Uri.EscapeDataString(assignmentPublicId)}/close",
            content: null,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AssignmentDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Empty close-assignment response.");
    }

    public async Task<PagedResponseDto<AttemptListItemDto>> ListAttemptsAsync(
        string assignmentPublicId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(
            $"api/tests/v1/assignments/{Uri.EscapeDataString(assignmentPublicId)}/attempts",
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PagedResponseDto<AttemptListItemDto>>(JsonOptions, cancellationToken)
            ?? new PagedResponseDto<AttemptListItemDto>([], 0);
    }

    public async Task<PagedResponseDto<ResultDto>> ListResultsAsync(
        string assignmentPublicId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(
            $"api/tests/v1/assignments/{Uri.EscapeDataString(assignmentPublicId)}/results",
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PagedResponseDto<ResultDto>>(JsonOptions, cancellationToken)
            ?? new PagedResponseDto<ResultDto>([], 0);
    }

    public async Task<ResultDto> GetAttemptResultAsync(
        string attemptPublicId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(
            $"api/tests/v1/attempts/{Uri.EscapeDataString(attemptPublicId)}/result",
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ResultDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Empty result response.");
    }

    public async Task<AttemptDto> GetAttemptAsync(
        string attemptPublicId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(
            $"api/tests/v1/attempts/{Uri.EscapeDataString(attemptPublicId)}",
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AttemptDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Empty attempt response.");
    }

    public void Dispose() => _http.Dispose();

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        string? error = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var errorNode))
            {
                error = errorNode.GetString();
            }
        }
        catch
        {
            // Keep raw body.
        }

        throw new InvalidOperationException(error ?? $"HTTP {(int)response.StatusCode}: {body}");
    }
}
