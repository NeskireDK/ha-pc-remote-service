using HaPcRemote.Service.Configuration;
using Microsoft.Extensions.Logging;

namespace HaPcRemote.Service.Services;

/// <summary>
/// Writes default "steam" and "steam-bigpicture" app entries to config on every startup
/// if Steam is installed and either entry is absent. Windows-only; no-op on other platforms.
/// </summary>
public static class SteamAppBootstrapper
{
    public static void BootstrapIfNeeded(
        ISteamPlatform platform,
        IConfigurationWriter writer,
        PcRemoteOptions currentOptions,
        ILogger logger)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var needsSteam = !currentOptions.Apps.ContainsKey("steam");
        var needsBigPicture = !currentOptions.Apps.ContainsKey("steam-bigpicture");

        if (!needsSteam && !needsBigPicture)
            return;

        var steamPath = platform.GetSteamPath();
        if (string.IsNullOrEmpty(steamPath))
            return;

        var exePath = Path.Combine(steamPath, "steam.exe");
        if (!File.Exists(exePath))
            return;

        if (needsSteam)
        {
            writer.SaveApp("steam", new AppDefinitionOptions
            {
                DisplayName = "Steam",
                ExePath = exePath,
                Arguments = "",
                ProcessName = "steam",
                UseShellExecute = false
            });

            logger.LogInformation("Auto-registered Steam app entry: {ExePath}", exePath);
        }

        if (needsBigPicture)
        {
            writer.SaveApp("steam-bigpicture", new AppDefinitionOptions
            {
                DisplayName = "Steam Big Picture",
                ExePath = exePath,
                Arguments = "-bigpicture",
                ProcessName = "steam",
                UseShellExecute = false
            });

            logger.LogInformation("Auto-registered Steam Big Picture app entry: {ExePath}", exePath);
        }
    }
}
