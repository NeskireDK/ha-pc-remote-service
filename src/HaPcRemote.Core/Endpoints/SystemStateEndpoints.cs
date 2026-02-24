using HaPcRemote.Service.Models;
using HaPcRemote.Service.Services;

namespace HaPcRemote.Service.Endpoints;

public static class SystemStateEndpoints
{
    public static IEndpointRouteBuilder MapSystemStateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/system/state", async (
            IAudioService audioService,
            IMonitorService monitorService,
            SteamService steamService,
            ModeService modeService,
            ILogger<SystemState> logger) =>
        {
            // Fire all async calls concurrently
            var audioTask = Task.Run(async () =>
            {
                var devices = await audioService.GetDevicesAsync();
                var current = devices.Find(d => d.IsDefault);
                return new AudioState
                {
                    Devices = devices,
                    Current = current?.Name,
                    Volume = current?.Volume
                };
            });

            var monitorsTask = Task.Run(() => monitorService.GetMonitorsAsync());

            var profilesTask = Task.Run(async () =>
            {
                var profiles = await monitorService.GetProfilesAsync();
                return profiles.Select(p => p.Name).ToList();
            });

            var steamGamesTask = Task.Run(() => steamService.GetGamesAsync());
            var runningGameTask = Task.Run(() => steamService.GetRunningGameAsync());

            try { await Task.WhenAll(audioTask, monitorsTask, profilesTask, steamGamesTask, runningGameTask); }
            catch { /* individual failures handled below */ }

            // Extract results — each in its own try/catch for partial failure
            AudioState? audio = null;
            try { audio = await audioTask; }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to get audio state"); }

            List<MonitorInfo>? monitors = null;
            try { monitors = await monitorsTask; }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to get monitors"); }

            List<string>? monitorProfiles = null;
            try { monitorProfiles = await profilesTask; }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to get monitor profiles"); }

            List<SteamGame>? steamGames = null;
            try { steamGames = await steamGamesTask; }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to get Steam games"); }

            SteamRunningGame? runningGame = null;
            try { runningGame = await runningGameTask; }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to get running Steam game"); }

            List<string>? modes = null;
            try { modes = modeService.GetModeNames().ToList(); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to get modes"); }

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
