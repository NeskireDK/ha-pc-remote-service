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
                return Results.Json(ApiResponse.Fail(ex.Message), AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        group.MapGet("/mac", () =>
        {
            var macs = WolService.GetMacAddresses();
            return Results.Json(ApiResponse.Ok(macs),
                AppJsonContext.Default.ApiResponseListMacAddressInfo);
        });

        group.MapPost("/wol/{mac}", async (string mac, ILogger<IPowerService> logger) =>
        {
            try
            {
                logger.LogInformation("WoL requested for MAC: {Mac}", mac);
                await WolService.SendWolAsync(mac);
                return Results.Json(ApiResponse.Ok($"WoL packet sent to {mac}"),
                    AppJsonContext.Default.ApiResponse);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "Invalid MAC address: {Mac}", mac);
                return Results.Json(ApiResponse.Fail(ex.Message), AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status400BadRequest);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send WoL packet to {Mac}", mac);
                return Results.Json(ApiResponse.Fail(ex.Message), AppJsonContext.Default.ApiResponse,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        return group;
    }
}
