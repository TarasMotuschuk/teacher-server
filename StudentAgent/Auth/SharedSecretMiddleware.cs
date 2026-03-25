using System.Net;
using StudentAgent.Services;

namespace StudentAgent.Auth;

public sealed class SharedSecretMiddleware
{
    private const string HeaderName = "X-Teacher-Secret";
    private readonly RequestDelegate _next;
    private readonly AgentSettingsStore _settingsStore;
    private readonly AgentLogService _logService;

    public SharedSecretMiddleware(RequestDelegate next, AgentSettingsStore settingsStore, AgentLogService logService)
    {
        _next = next;
        _settingsStore = settingsStore;
        _logService = logService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        var expectedSecret = _settingsStore.Current.SharedSecret;
        if (!context.Request.Headers.TryGetValue(HeaderName, out var headerValue) ||
            !string.Equals(headerValue.ToString(), expectedSecret, StringComparison.Ordinal))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            _logService.LogWarning($"Unauthorized access attempt for {context.Request.Method} {context.Request.Path}");
            await context.Response.WriteAsJsonAsync(new { error = "Invalid shared secret." });
            return;
        }

        await _next(context);
    }
}
