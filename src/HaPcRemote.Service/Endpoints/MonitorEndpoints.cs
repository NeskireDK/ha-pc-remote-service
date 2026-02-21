using HaPcRemote.Service.Models;
using HaPcRemote.Service.Services;

namespace HaPcRemote.Service.Endpoints;

public static class MonitorEndpoints
{
    public static RouteGroupBuilder MapMonitorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/monitor");

        // ── Monitor control endpoints ────────────────────────────────

        group.MapGet("/list", async (MonitorService monitorService) =>
        {
            var monitors = await monitorService.GetMonitorsAsync();
            return Results.Json(
                ApiResponse.Ok<List<MonitorInfo>>(monitors),
                AppJsonContext.Default.ApiResponseListMonitorInfo);
        });

        group.MapPost("/solo/{id}", async (string id, MonitorService monitorService,
            ILogger<MonitorService> logger) =>
        {
            try
            {
                logger.LogInformation("Solo monitor '{Id}' requested", id);
                await monitorService.SoloMonitorAsync(id);
                return Results.Json(
                    ApiResponse.Ok($"Solo monitor '{id}' applied"),
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
                logger.LogError(ex, "Failed to solo monitor '{Id}'", id);
                return Results.Json(
                    ApiResponse.Fail("Internal server error"),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        group.MapPost("/enable/{id}", async (string id, MonitorService monitorService,
            ILogger<MonitorService> logger) =>
        {
            try
            {
                logger.LogInformation("Enable monitor '{Id}' requested", id);
                await monitorService.EnableMonitorAsync(id);
                return Results.Json(
                    ApiResponse.Ok($"Monitor '{id}' enabled"),
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
                logger.LogError(ex, "Failed to enable monitor '{Id}'", id);
                return Results.Json(
                    ApiResponse.Fail("Internal server error"),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        group.MapPost("/disable/{id}", async (string id, MonitorService monitorService,
            ILogger<MonitorService> logger) =>
        {
            try
            {
                logger.LogInformation("Disable monitor '{Id}' requested", id);
                await monitorService.DisableMonitorAsync(id);
                return Results.Json(
                    ApiResponse.Ok($"Monitor '{id}' disabled"),
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
                logger.LogError(ex, "Failed to disable monitor '{Id}'", id);
                return Results.Json(
                    ApiResponse.Fail("Internal server error"),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        group.MapPost("/primary/{id}", async (string id, MonitorService monitorService,
            ILogger<MonitorService> logger) =>
        {
            try
            {
                logger.LogInformation("Set primary monitor '{Id}' requested", id);
                await monitorService.SetPrimaryAsync(id);
                return Results.Json(
                    ApiResponse.Ok($"Monitor '{id}' set as primary"),
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
                logger.LogError(ex, "Failed to set primary monitor '{Id}'", id);
                return Results.Json(
                    ApiResponse.Fail("Internal server error"),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        // ── Profile endpoints ────────────────────────────────────────

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
                    ApiResponse.Fail("Internal server error"),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        return group;
    }
}
