using StudentAgent.Services;

namespace StudentAgent.Auth;

public sealed class GlobalExceptionLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AgentLogService _logService;

    public GlobalExceptionLoggingMiddleware(RequestDelegate next, AgentLogService logService)
    {
        _next = next;
        _logService = logService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logService.LogError($"Unhandled request exception for {context.Request.Method} {context.Request.Path}: {ex}");

            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Internal server error.",
                    detail = ex.Message
                });
            }
        }
    }
}
