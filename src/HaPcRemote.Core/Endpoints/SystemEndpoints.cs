using HaPcRemote.Service.Models;
using HaPcRemote.Service.Services;

namespace HaPcRemote.Service.Endpoints;

public static class SystemEndpoints
{
    public static RouteGroupBuilder MapSystemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/system");

        group.MapPost("/sleep", async (IPowerService powerService, ILogger<IPowerService> logger) =>
        {
            try
            {
                logger.LogInformation("Sleep requested");
                await powerService.SleepAsync();
                return Results.Json(ApiResponse.Ok("Sleep initiated"), AppJsonContext.Default.ApiResponse);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initiate sleep");
                return Results.Json(ApiResponse.Fail("Internal server error"), AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        return group;
    }
}
