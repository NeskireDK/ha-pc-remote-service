using HaPcRemote.Service.Models;
using HaPcRemote.Service.Services;

namespace HaPcRemote.Service.Endpoints;

public static class SteamEndpoints
{
    public static RouteGroupBuilder MapSteamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/steam");

        group.MapGet("/games", async (SteamService steamService, ILogger<SteamService> logger) =>
        {
            try
            {
                var games = await steamService.GetGamesAsync();
                return Results.Json(
                    ApiResponse.Ok<List<SteamGame>>(games),
                    AppJsonContext.Default.ApiResponseListSteamGame);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get Steam games");
                return Results.Json(
                    ApiResponse.Fail("Internal server error"),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        group.MapGet("/running", async (SteamService steamService, ILogger<SteamService> logger) =>
        {
            try
            {
                var running = await steamService.GetRunningGameAsync();
                return Results.Json(
                    ApiResponse.Ok(running),
                    AppJsonContext.Default.ApiResponseSteamRunningGame);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get running Steam game");
                return Results.Json(
                    ApiResponse.Fail("Internal server error"),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        group.MapPost("/run/{appId:int}", async (int appId, SteamService steamService,
            ILogger<SteamService> logger) =>
        {
            try
            {
                logger.LogInformation("Launch Steam game requested: {AppId}", appId);
                var result = await steamService.LaunchGameAsync(appId);
                return Results.Json(
                    ApiResponse.Ok(result),
                    AppJsonContext.Default.ApiResponseSteamRunningGame);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to launch Steam game {AppId}", appId);
                return Results.Json(
                    ApiResponse.Fail("Internal server error"),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        group.MapPost("/stop", async (SteamService steamService, ILogger<SteamService> logger) =>
        {
            try
            {
                logger.LogInformation("Stop Steam game requested");
                await steamService.StopGameAsync();
                return Results.Json(
                    ApiResponse.Ok("Steam game stopped"),
                    AppJsonContext.Default.ApiResponse);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to stop Steam game");
                return Results.Json(
                    ApiResponse.Fail("Internal server error"),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        return group;
    }
}
