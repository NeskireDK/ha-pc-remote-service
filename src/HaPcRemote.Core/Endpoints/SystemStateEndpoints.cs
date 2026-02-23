using HaPcRemote.Service.Models;
using HaPcRemote.Service.Services;

namespace HaPcRemote.Service.Endpoints;

public static class SystemStateEndpoints
{
    public static IEndpointRouteBuilder MapSystemStateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/system/state", async (
            AudioService audioService,
            MonitorService monitorService,
            SteamService steamService,
            ModeService modeService,
            ILogger<SystemState> logger) =>
        {
            AudioState? audio = null;
            List<MonitorInfo>? monitors = null;
            List<string>? monitorProfiles = null;
            List<SteamGame>? steamGames = null;
            SteamGame? runningGame = null;
            List<string>? modes = null;

            try
            {
                var devices = await audioService.GetDevicesAsync();
                var current = devices.Find(d => d.IsDefault);
                audio = new AudioState
                {
                    Devices = devices.Select(d => d.Name).ToList(),
                    Current = current?.Name,
                    Volume = current?.Volume
                };
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to get audio state");
            }

            try
            {
                monitors = await monitorService.GetMonitorsAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to get monitors");
            }

            try
            {
                var profiles = await monitorService.GetProfilesAsync();
                monitorProfiles = profiles.Select(p => p.Name).ToList();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to get monitor profiles");
            }

            try
            {
                steamGames = await steamService.GetGamesAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to get Steam games");
            }

            try
            {
                var running = await steamService.GetRunningGameAsync();
                if (running is not null)
                    runningGame = new SteamGame { AppId = running.AppId, Name = running.Name, LastPlayed = 0 };
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to get running Steam game");
            }

            try
            {
                modes = modeService.GetModeNames().ToList();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to get modes");
            }

            var state = new SystemState
            {
                Audio = audio,
                Monitors = monitors,
                MonitorProfiles = monitorProfiles,
                SteamGames = steamGames,
                RunningGame = runningGame,
                Modes = modes
            };

            return Results.Json(
                ApiResponse.Ok(state),
                AppJsonContext.Default.ApiResponseSystemState);
        });

        return endpoints;
    }
}
