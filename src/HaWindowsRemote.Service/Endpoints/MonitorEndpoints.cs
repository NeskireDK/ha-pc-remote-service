using HaWindowsRemote.Service.Models;
using HaWindowsRemote.Service.Services;

namespace HaWindowsRemote.Service.Endpoints;

public static class MonitorEndpoints
{
    public static RouteGroupBuilder MapMonitorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/monitor");

        group.MapGet("/profiles", async (MonitorService monitorService) =>
        {
            var profiles = await monitorService.GetProfilesAsync();
            return Results.Json(
                ApiResponse.Ok<List<MonitorProfile>>(profiles),
                AppJsonContext.Default.ApiResponseListMonitorProfile);
        });

        group.MapPost("/set/{profile}", async (string profile, MonitorService monitorService,
            ILogger<MonitorService> logger) =>
        {
            try
            {
                logger.LogInformation("Monitor profile '{Profile}' requested", profile);
                await monitorService.ApplyProfileAsync(profile);
                return Results.Json(
                    ApiResponse.Ok($"Monitor profile '{profile}' applied"),
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
                logger.LogError(ex, "Failed to apply monitor profile '{Profile}'", profile);
                return Results.Json(
                    ApiResponse.Fail(ex.Message),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        return group;
    }
}
