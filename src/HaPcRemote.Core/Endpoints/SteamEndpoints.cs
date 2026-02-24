using HaPcRemote.Service.Middleware;
using HaPcRemote.Service.Models;
using HaPcRemote.Service.Services;

namespace HaPcRemote.Service.Endpoints;

public static class SteamEndpoints
{
    public static RouteGroupBuilder MapSteamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/steam");
        group.AddEndpointFilter<EndpointExceptionFilter>();

        group.MapGet("/games", async (ISteamService steamService) =>
        {
            var games = await steamService.GetGamesAsync();
            return Results.Json(
                ApiResponse.Ok<List<SteamGame>>(games),
                AppJsonContext.Default.ApiResponseListSteamGame);
        });

        group.MapGet("/running", async (ISteamService steamService) =>
        {
            var running = await steamService.GetRunningGameAsync();
            return Results.Json(
                ApiResponse.Ok(running),
                AppJsonContext.Default.ApiResponseSteamRunningGame);
        });

        group.MapPost("/run/{appId:int}", async (int appId, ISteamService steamService,
            ILogger<SteamService> logger) =>
        {
            logger.LogInformation("Launch Steam game requested: {AppId}", appId);
            var result = await steamService.LaunchGameAsync(appId);
            return Results.Json(
                ApiResponse.Ok(result),
                AppJsonContext.Default.ApiResponseSteamRunningGame);
        });

        group.MapPost("/stop", async (ISteamService steamService, ILogger<SteamService> logger) =>
        {
            logger.LogInformation("Stop Steam game requested");
            await steamService.StopGameAsync();
            return Results.Json(
                ApiResponse.Ok("Steam game stopped"),
                AppJsonContext.Default.ApiResponse);
        });

        return group;
    }
}
