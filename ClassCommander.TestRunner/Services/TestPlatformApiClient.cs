using System.Net.Http.Json;
using System.Text.Json;
using ClassCommander.Testing.Core.Serialization;
using Teacher.Common.Contracts.Testing;

namespace ClassCommander.TestRunner.Services;

internal sealed class TestPlatformApiClient : IDisposable
{
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

    public async Task<ResolveStudentResponse> ResolveAsync(ResolveStudentRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("api/tests/v1/student/resolve", request, TestPlatformJson.Options, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ResolveStudentResponse>(TestPlatformJson.Options, cancellationToken)
            ?? throw new InvalidOperationException("Empty resolve response.");
    }

    public async Task<StartAttemptResponse> StartAttemptAsync(StartAttemptRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("api/tests/v1/student/attempts", request, TestPlatformJson.Options, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<StartAttemptResponse>(TestPlatformJson.Options, cancellationToken)
            ?? throw new InvalidOperationException("Empty start-attempt response.");
    }

    public async Task SaveProgressAsync(
        string attemptPublicId,
        string attemptToken,
        SaveAttemptProgressRequest request,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, $"api/tests/v1/student/attempts/{Uri.EscapeDataString(attemptPublicId)}/progress")
        {
            Content = JsonContent.Create(request, options: TestPlatformJson.Options),
        };
        message.Headers.Add("X-Attempt-Token", attemptToken);
        using var response = await _http.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<SubmitAttemptResponse> SubmitAsync(
        string attemptPublicId,
        string attemptToken,
        SubmitAttemptRequest request,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"api/tests/v1/student/attempts/{Uri.EscapeDataString(attemptPublicId)}/submit")
        {
            Content = JsonContent.Create(request, options: TestPlatformJson.Options),
        };
        message.Headers.Add("X-Attempt-Token", attemptToken);
        using var response = await _http.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SubmitAttemptResponse>(TestPlatformJson.Options, cancellationToken)
            ?? throw new InvalidOperationException("Empty submit response.");
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
