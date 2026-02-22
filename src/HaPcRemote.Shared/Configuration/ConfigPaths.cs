namespace HaPcRemote.Shared.Configuration;

public static class ConfigPaths
{
    private const string AppName = "HaPcRemote";

    /// <summary>
    /// Returns a writable directory for storing runtime-generated configuration
    /// (API keys, etc.) that survives updates and works in read-only install
    /// locations like C:\Program Files.
    ///
    /// Windows: %ProgramData%\HaPcRemote
    /// Fallback: exe directory (portable / development)
    /// </summary>
    public static string GetWritableConfigDir()
    {
        var commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrEmpty(commonData))
            return Path.Combine(commonData, AppName);

        // Fallback for platforms where CommonApplicationData is not available
        return AppContext.BaseDirectory;
    }

    public static string GetWritableConfigPath()
        => Path.Combine(GetWritableConfigDir(), "appsettings.json");

    public static string GetLogFilePath()
        => Path.Combine(GetWritableConfigDir(), "service.log");
}
