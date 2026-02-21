using HaWindowsRemote.Service.Models;
using HaWindowsRemote.Service.Services;

namespace HaWindowsRemote.Service.Endpoints;

public static class AppEndpoints
{
    public static RouteGroupBuilder MapAppEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/app");

        group.MapGet("/status", async (AppService appService) =>
        {
            var statuses = await appService.GetAllStatusesAsync();
            return Results.Json(
                ApiResponse.Ok<List<AppInfo>>(statuses),
                AppJsonContext.Default.ApiResponseListAppInfo);
        });

        group.MapPost("/launch/{appKey}", async (string appKey, AppService appService,
            ILogger<AppService> logger) =>
        {
            try
            {
                logger.LogInformation("Launch requested for app '{AppKey}'", appKey);
                await appService.LaunchAsync(appKey);
                return Results.Json(
                    ApiResponse.Ok($"App '{appKey}' launched"),
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
                logger.LogError(ex, "Failed to launch app '{AppKey}'", appKey);
                return Results.Json(
                    ApiResponse.Fail(ex.Message),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        group.MapPost("/kill/{appKey}", async (string appKey, AppService appService,
            ILogger<AppService> logger) =>
        {
            try
            {
                logger.LogInformation("Kill requested for app '{AppKey}'", appKey);
                await appService.KillAsync(appKey);
                return Results.Json(
                    ApiResponse.Ok($"App '{appKey}' killed"),
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
                logger.LogError(ex, "Failed to kill app '{AppKey}'", appKey);
                return Results.Json(
                    ApiResponse.Fail(ex.Message),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        group.MapGet("/status/{appKey}", async (string appKey, AppService appService) =>
        {
            try
            {
                var status = await appService.GetStatusAsync(appKey);
                return Results.Json(
                    ApiResponse.Ok(status),
                    AppJsonContext.Default.ApiResponseAppInfo);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.Json(
                    ApiResponse.Fail(ex.Message),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status404NotFound);
            }
        });

        return group;
    }
}
