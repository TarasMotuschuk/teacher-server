using StudentAgent.Services;

namespace StudentAgent.Auth;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AgentLogService _logService;

    public RequestLoggingMiddleware(RequestDelegate next, AgentLogService logService)
    {
        _next = next;
        _logService = logService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (context.Request.Path.StartsWithSegments("/health"))
        {
            return;
        }

        try
        {
            _logService.LogInfo($"{context.Request.Method} {context.Request.Path} -> {context.Response.StatusCode}");
        }
        catch
        {
            // Request logging must never affect the HTTP response.
        }
    }
}
