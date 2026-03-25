using System.Net;
using Microsoft.Extensions.Options;

namespace StudentAgent.Auth;

public sealed class SharedSecretMiddleware
{
    private const string HeaderName = "X-Teacher-Secret";
    private readonly RequestDelegate _next;
    private readonly AgentOptions _options;

    public SharedSecretMiddleware(RequestDelegate next, IOptions<AgentOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var headerValue) ||
            !string.Equals(headerValue.ToString(), _options.SharedSecret, StringComparison.Ordinal))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid shared secret." });
            return;
        }

        await _next(context);
    }
}
