using HaPcRemote.Service.Middleware;
using HaPcRemote.Service.Models;
using HaPcRemote.Service.Services;

namespace HaPcRemote.Service.Endpoints;

public static class AudioEndpoints
{
    public static RouteGroupBuilder MapAudioEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/audio");
        group.AddEndpointFilter<EndpointExceptionFilter>();

        group.MapGet("/devices", async (IAudioService audioService) =>
        {
            var devices = await audioService.GetDevicesAsync();
            return Results.Json(
                ApiResponse.Ok<List<AudioDevice>>(devices),
                AppJsonContext.Default.ApiResponseListAudioDevice);
        });

        group.MapGet("/current", async (IAudioService audioService) =>
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
        });

        group.MapPost("/set/{deviceName}", async (string deviceName, IAudioService audioService,
            ILogger<IAudioService> logger) =>
        {
            logger.LogInformation("Set default audio device requested: '{DeviceName}'", deviceName);
            await audioService.SetDefaultDeviceAsync(deviceName);
            return Results.Json(
                ApiResponse.Ok($"Default device set to '{deviceName}'"),
                AppJsonContext.Default.ApiResponse);
        });

        group.MapPost("/volume/{level:int}", async (int level, IAudioService audioService,
            ILogger<IAudioService> logger) =>
        {
            if (level < 0 || level > 100)
            {
                return Results.Json(
                    ApiResponse.Fail("Volume must be between 0 and 100"),
                    AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            logger.LogInformation("Set volume requested: {Level}", level);
            await audioService.SetVolumeAsync(level);
            return Results.Json(
                ApiResponse.Ok($"Volume set to {level}"),
                AppJsonContext.Default.ApiResponse);
        });

        return group;
    }
}
