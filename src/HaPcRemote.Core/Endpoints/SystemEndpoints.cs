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

        return group;
    }
}
