using HaPcRemote.Service.Models;
using HaPcRemote.Service.Services;

namespace HaPcRemote.Service.Endpoints;

public static class ModeEndpoints
{
    public static IEndpointRouteBuilder MapModeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/system/modes", (ModeService modeService) =>
        {
            var names = modeService.GetModeNames();
            return Results.Json(
                ApiResponse.Ok<IReadOnlyList<string>>(names),
                AppJsonContext.Default.ApiResponseIReadOnlyListString);
        });

        endpoints.MapPost("/api/system/mode/{modeName}", async (string modeName, ModeService modeService,
            ILogger<ModeService> logger) =>
        {
            try
            {
                logger.LogInformation("Apply mode '{ModeName}' requested", modeName);
                await modeService.ApplyModeAsync(modeName);
                return Results.Json(
                    ApiResponse.Ok($"Mode '{modeName}' applied"),
                    AppJsonContext.Default.ApiResponse);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.Json(
                    ApiResponse.Fail(ex.Message),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status404NotFound);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to apply mode '{ModeName}'", modeName);
                return Results.Json(
                    ApiResponse.Fail("Internal server error"),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        return endpoints;
    }
}
