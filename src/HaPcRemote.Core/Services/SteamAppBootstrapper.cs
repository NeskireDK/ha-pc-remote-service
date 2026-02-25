using HaPcRemote.Service.Configuration;
using Microsoft.Extensions.Logging;

namespace HaPcRemote.Service.Services;

/// <summary>
/// Writes a default "steam" app entry to config on first run if Steam is installed
/// and no "steam" key is present. Windows-only; no-op on other platforms.
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

        if (currentOptions.Apps.ContainsKey("steam"))
            return;

        var steamPath = platform.GetSteamPath();
        if (string.IsNullOrEmpty(steamPath))
            return;

        var exePath = Path.Combine(steamPath, "steam.exe");
        if (!File.Exists(exePath))
            return;

        writer.SaveApp("steam", new AppDefinitionOptions
        {
            DisplayName = "Steam",
            ExePath = exePath,
            Arguments = "-bigpicture",
            ProcessName = "steam",
            UseShellExecute = false
        });

        logger.LogInformation("Auto-registered Steam app entry: {ExePath}", exePath);
    }
}
