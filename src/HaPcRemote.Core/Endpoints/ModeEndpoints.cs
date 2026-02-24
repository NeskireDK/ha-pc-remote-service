using HaPcRemote.Service.Middleware;
using HaPcRemote.Service.Models;
using HaPcRemote.Service.Services;

namespace HaPcRemote.Service.Endpoints;

public static class ModeEndpoints
{
    public static IEndpointRouteBuilder MapModeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/system/modes", (IModeService modeService) =>
        {
            var names = modeService.GetModeNames();
            return Results.Json(
                ApiResponse.Ok<IReadOnlyList<string>>(names),
                AppJsonContext.Default.ApiResponseIReadOnlyListString);
        }).AddEndpointFilter<EndpointExceptionFilter>();

        endpoints.MapPost("/api/system/mode/{modeName}", async (string modeName, IModeService modeService,
            ILogger<ModeService> logger) =>
        {
            logger.LogInformation("Apply mode '{ModeName}' requested", modeName);
            await modeService.ApplyModeAsync(modeName);
            return Results.Json(
                ApiResponse.Ok($"Mode '{modeName}' applied"),
                AppJsonContext.Default.ApiResponse);
        }).AddEndpointFilter<EndpointExceptionFilter>();

        return endpoints;
    }
}
