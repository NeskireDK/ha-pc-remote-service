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
        {
            var dir = Path.Combine(commonData, AppName);

            // On non-Windows platforms, CommonApplicationData may resolve to a
            // system directory (e.g. /usr/share) that isn't writable by the
            // current user.  Only use it when we can actually write there.
            if (OperatingSystem.IsWindows() || IsDirectoryWritable(dir))
                return dir;
        }

        // Fallback for platforms where CommonApplicationData is not available
        return AppContext.BaseDirectory;
    }

    public static string GetWritableConfigPath()
        => Path.Combine(GetWritableConfigDir(), "appsettings.json");

    public static string GetLogFilePath()
        => Path.Combine(GetWritableConfigDir(), "service.log");

    private static bool IsDirectoryWritable(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
