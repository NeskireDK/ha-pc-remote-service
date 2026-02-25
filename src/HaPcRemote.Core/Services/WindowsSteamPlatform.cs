using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace HaPcRemote.Service.Services;

[SupportedOSPlatform("windows")]
public class WindowsSteamPlatform(ILogger<WindowsSteamPlatform> logger) : ISteamPlatform
{
    public string? GetSteamPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
        return key?.GetValue("SteamPath") as string;
    }

    public string? GetSteamUserId()
    {
        var steamPath = GetSteamPath();
        return steamPath != null ? SteamUserIdResolver.Resolve(steamPath) : null;
    }

    public int GetRunningAppId()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
        return key?.GetValue("RunningAppID") is int appId ? appId : 0;
    }

    public bool IsSteamRunning() => Process.GetProcessesByName("steam").Length > 0;

    public void LaunchSteamUrl(string url)
    {
        logger.LogInformation("Launching Steam URL: {Url}", url);
        using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        if (process is null)
        {
            logger.LogWarning(
                "Steam URL launch returned null process handle — Steam may not be installed or the steam:// protocol is not registered: {Url}",
                url);
        }
    }

    public void KillProcessesInDirectory(string directory)
    {
        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                var path = proc.MainModule?.FileName;
                if (path != null && path.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
                {
                    proc.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Access denied for system processes, or process already exited
            }
            finally
            {
                proc.Dispose();
            }
        }
    }
}
