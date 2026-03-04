using HaPcRemote.Service.Middleware;
using HaPcRemote.Service.Models;
using HaPcRemote.Service.Services;

namespace HaPcRemote.Service.Endpoints;

public static class SystemEndpoints
{
    public static RouteGroupBuilder MapSystemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/system");
        group.AddEndpointFilter<EndpointExceptionFilter>();

        group.MapPost("/sleep", async (IPowerService powerService, ILogger<IPowerService> logger) =>
        {
            logger.LogInformation("Sleep requested");
            await powerService.SleepAsync();
            return Results.Json(ApiResponse.Ok("Sleep initiated"), AppJsonContext.Default.ApiResponse);
        });

        group.MapGet("/idle", (IIdleService idleService) =>
        {
            var seconds = idleService.GetIdleSeconds();
            if (seconds is null)
                return Results.Json(
                    ApiResponse.Fail("Idle detection unavailable"),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            return Results.Json(ApiResponse.Ok(seconds.Value), AppJsonContext.Default.ApiResponseInt32);
        });

        group.MapPost("/restart", (IRestartService restartService, ILogger<IRestartService> logger) =>
        {
            logger.LogInformation("Restart requested via API");
            restartService.ScheduleRestart();
            return Results.Json(ApiResponse.Ok("Restart scheduled"), AppJsonContext.Default.ApiResponse);
        });

        group.MapPost("/update", async (IUpdateService updateService, ILogger<IUpdateService> logger, CancellationToken ct) =>
        {
            logger.LogInformation("Update requested via API");
            var result = await updateService.CheckAndApplyAsync(ct);
            return result.Status switch
            {
                UpdateStatus.Failed => Results.Json(
                    new ApiResponse<UpdateResult> { Success = false, Data = result, Message = result.Message },
                    AppJsonContext.Default.ApiResponseUpdateResult,
                    statusCode: StatusCodes.Status500InternalServerError),
                _ => Results.Json(
                    ApiResponse.Ok(result, result.Message),
                    AppJsonContext.Default.ApiResponseUpdateResult)
            };
        });

        return group;
    }
}
