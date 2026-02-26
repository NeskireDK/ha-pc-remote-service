using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Models;
using HaPcRemote.Service.Services;
using Microsoft.Extensions.Options;

namespace HaPcRemote.Service.Endpoints;

public static class PowerEndpoints
{
    public static RouteGroupBuilder MapPowerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/system/power");
        group.AddEndpointFilter<Middleware.EndpointExceptionFilter>();

        group.MapGet("/", (IOptionsMonitor<PcRemoteOptions> options) =>
        {
            var power = options.CurrentValue.Power;
            return Results.Json(
                ApiResponse.Ok(new PowerConfig { AutoSleepAfterMinutes = power.AutoSleepAfterMinutes }),
                AppJsonContext.Default.ApiResponsePowerConfig);
        });

        group.MapPut("/", (PowerConfig body, IConfigurationWriter writer) =>
        {
            var minutes = Math.Clamp(body.AutoSleepAfterMinutes, 0, 480);
            writer.SavePowerSettings(new PowerSettings { AutoSleepAfterMinutes = minutes });
            return Results.Json(ApiResponse.Ok("Power settings saved"), AppJsonContext.Default.ApiResponse);
        });

        return group;
    }
}
