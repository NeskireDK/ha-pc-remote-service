using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Models;
using Microsoft.Extensions.Options;

namespace HaPcRemote.Service.Middleware;

public sealed class ApiKeyMiddleware
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    private static readonly string[] ExemptPaths = ["/api/health"];

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IOptionsMonitor<PcRemoteOptions> options)
    {
        var config = options.CurrentValue;

        if (!config.Auth.Enabled)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;

        if (ExemptPaths.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey) ||
            string.IsNullOrEmpty(providedKey))
        {
            _logger.LogWarning("Request to {Path} missing API key", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(
                ApiResponse.Fail("Missing API key"), AppJsonContext.Default.ApiResponse);
            return;
        }

        if (!string.Equals(providedKey, config.Auth.ApiKey, StringComparison.Ordinal))
        {
            _logger.LogWarning("Request to {Path} with invalid API key", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(
                ApiResponse.Fail("Invalid API key"), AppJsonContext.Default.ApiResponse);
            return;
        }

        await _next(context);
    }
}
