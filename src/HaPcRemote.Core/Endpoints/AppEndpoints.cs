using HaPcRemote.Service.Middleware;
using HaPcRemote.Service.Models;
using HaPcRemote.Service.Services;

namespace HaPcRemote.Service.Endpoints;

public static class AppEndpoints
{
    public static RouteGroupBuilder MapAppEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/app");
        group.AddEndpointFilter<EndpointExceptionFilter>();

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
            logger.LogInformation("Launch requested for app '{AppKey}'", appKey);
            await appService.LaunchAsync(appKey);
            return Results.Json(
                ApiResponse.Ok($"App '{appKey}' launched"),
                AppJsonContext.Default.ApiResponse);
        });

        group.MapPost("/kill/{appKey}", async (string appKey, AppService appService,
            ILogger<AppService> logger) =>
        {
            logger.LogInformation("Kill requested for app '{AppKey}'", appKey);
            await appService.KillAsync(appKey);
            return Results.Json(
                ApiResponse.Ok($"App '{appKey}' killed"),
                AppJsonContext.Default.ApiResponse);
        });

        group.MapGet("/status/{appKey}", async (string appKey, AppService appService) =>
        {
            var status = await appService.GetStatusAsync(appKey);
            return Results.Json(
                ApiResponse.Ok(status),
                AppJsonContext.Default.ApiResponseAppInfo);
        });

        return group;
    }
}
