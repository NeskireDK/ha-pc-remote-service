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
            ISteamService steamService,
            IModeService modeService,
            IIdleService idleService,
            ILogger<SystemState> logger) =>
        {
            // Fire all async calls concurrently
            var audioTask = GetAudioStateAsync(audioService);
            var monitorsTask = monitorService.GetMonitorsAsync();
            var profilesTask = GetProfileNamesAsync(monitorService);
            var steamGamesTask = steamService.GetGamesAsync();
            var runningGameTask = steamService.GetRunningGameAsync();

            try { await Task.WhenAll(audioTask, monitorsTask, profilesTask, steamGamesTask, runningGameTask); }
            catch (Exception ex) { logger.LogDebug(ex, "One or more state queries failed"); }

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

            int? idleSeconds = null;
            try { idleSeconds = idleService.GetIdleSeconds(); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to get idle duration"); }

            SteamBindings? steamBindings = null;
            try { steamBindings = steamService.GetBindings(); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to get steam bindings"); }

            bool? steamReady = null;
            try { steamReady = steamService.IsSteamRunning(); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to get Steam ready state"); }

            var state = new SystemState
            {
                Audio = audio,
                Monitors = monitors,
                MonitorProfiles = monitorProfiles,
                SteamGames = steamGames,
                RunningGame = runningGame,
                Modes = modes,
                IdleSeconds = idleSeconds,
                SteamBindings = steamBindings,
                SteamReady = steamReady
            };

            return Results.Json(
                ApiResponse.Ok(state),
                AppJsonContext.Default.ApiResponseSystemState);
        });

        return endpoints;
    }

    private static async Task<AudioState> GetAudioStateAsync(IAudioService audioService)
    {
        var devices = await audioService.GetDevicesAsync();
        var current = devices.Find(d => d.IsDefault);
        return new AudioState
        {
            Devices = devices,
            Current = current?.Name,
            Volume = current?.Volume
        };
    }

    private static async Task<List<string>> GetProfileNamesAsync(IMonitorService monitorService)
    {
        var profiles = await monitorService.GetProfilesAsync();
        return profiles.Select(p => p.Name).ToList();
    }
}
