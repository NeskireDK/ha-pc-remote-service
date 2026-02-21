using HaPcRemote.Service.Models;
using HaPcRemote.Service.Services;

namespace HaPcRemote.Service.Endpoints;

public static class AudioEndpoints
{
    public static RouteGroupBuilder MapAudioEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/audio");

        group.MapGet("/devices", async (AudioService audioService, ILogger<AudioService> logger) =>
        {
            try
            {
                var devices = await audioService.GetDevicesAsync();
                return Results.Json(
                    ApiResponse.Ok<List<AudioDevice>>(devices),
                    AppJsonContext.Default.ApiResponseListAudioDevice);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get audio devices");
                return Results.Json(
                    ApiResponse.Fail("Internal server error"),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        group.MapGet("/current", async (AudioService audioService, ILogger<AudioService> logger) =>
        {
            try
            {
                var device = await audioService.GetCurrentDeviceAsync();
                if (device is null)
                {
                    return Results.Json(
                        ApiResponse.Fail("No default audio device found"),
                        AppJsonContext.Default.ApiResponse,
                        statusCode: StatusCodes.Status404NotFound);
                }

                return Results.Json(
                    ApiResponse.Ok(device),
                    AppJsonContext.Default.ApiResponseAudioDevice);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get current audio device");
                return Results.Json(
                    ApiResponse.Fail("Internal server error"),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        group.MapPost("/set/{deviceName}", async (string deviceName, AudioService audioService,
            ILogger<AudioService> logger) =>
        {
            try
            {
                logger.LogInformation("Set default audio device requested: '{DeviceName}'", deviceName);
                await audioService.SetDefaultDeviceAsync(deviceName);
                return Results.Json(
                    ApiResponse.Ok($"Default device set to '{deviceName}'"),
                    AppJsonContext.Default.ApiResponse);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to set default audio device '{DeviceName}'", deviceName);
                return Results.Json(
                    ApiResponse.Fail("Internal server error"),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        group.MapPost("/volume/{level:int}", async (int level, AudioService audioService,
            ILogger<AudioService> logger) =>
        {
            if (level < 0 || level > 100)
            {
                return Results.Json(
                    ApiResponse.Fail("Volume must be between 0 and 100"),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                logger.LogInformation("Set volume requested: {Level}", level);
                await audioService.SetVolumeAsync(level);
                return Results.Json(
                    ApiResponse.Ok($"Volume set to {level}"),
                    AppJsonContext.Default.ApiResponse);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to set volume to {Level}", level);
                return Results.Json(
                    ApiResponse.Fail("Internal server error"),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        return group;
    }
}
