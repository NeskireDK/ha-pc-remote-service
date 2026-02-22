using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace HaPcRemote.Service.Services;

[SupportedOSPlatform("windows")]
public class WindowsSteamPlatform : ISteamPlatform
{
    public string? GetSteamPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
        return key?.GetValue("SteamPath") as string;
    }

    public int GetRunningAppId()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
        var value = key?.GetValue("RunningAppID");
        return value is int appId ? appId : 0;
    }

    public void LaunchSteamUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
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
