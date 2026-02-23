namespace HaPcRemote.Shared.Configuration;

public static class ConfigPaths
{
    private const string AppName = "HaPcRemote";

    /// <summary>
    /// Returns a writable directory for storing runtime-generated configuration.
    /// Windows: %AppData%\HaPcRemote
    /// Linux: $XDG_CONFIG_HOME/HaPcRemote or ~/.config/HaPcRemote
    /// Fallback: exe directory
    /// </summary>
    public static string GetWritableConfigDir()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
                return Path.Combine(appData, AppName);
        }

        if (OperatingSystem.IsLinux())
        {
            var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (!string.IsNullOrEmpty(xdgConfigHome))
                return Path.Combine(xdgConfigHome, AppName);

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home))
                return Path.Combine(home, ".config", AppName);
        }

        return AppContext.BaseDirectory;
    }

    public static string GetWritableConfigPath()
        => Path.Combine(GetWritableConfigDir(), "appsettings.json");

    public static string GetTraySettingsPath()
        => Path.Combine(GetWritableConfigDir(), "tray-settings.json");

    public static string GetLogFilePath()
        => Path.Combine(GetWritableConfigDir(), "service.log");
}
