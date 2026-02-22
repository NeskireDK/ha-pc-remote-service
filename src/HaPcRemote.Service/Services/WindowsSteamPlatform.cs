using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace HaPcRemote.Service.Services;

[SupportedOSPlatform("windows")]
public class WindowsSteamPlatform : ISteamPlatform
{
    public string? GetSteamPath()
    {
        // HKCU works when running in a user session; falls back to HKLM when
        // running as SYSTEM (Windows Service) where HKCU is the system hive.
        using var hkcuKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
        if (hkcuKey?.GetValue("SteamPath") is string hkcuPath)
            return hkcuPath;

        // Steam (32-bit) on 64-bit Windows writes install path to WOW6432Node.
        using var wow64Key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
        if (wow64Key?.GetValue("InstallPath") is string wow64Path)
            return wow64Path;

        using var hklmKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
        return hklmKey?.GetValue("InstallPath") as string;
    }

    public int GetRunningAppId()
    {
        // HKCU works in user session.
        using var hkcuKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
        if (hkcuKey?.GetValue("RunningAppID") is int hkcuAppId)
            return hkcuAppId;

        // When running as SYSTEM, enumerate all user hives to find a logged-in
        // user that has Steam running.
        foreach (var sid in Registry.Users.GetSubKeyNames())
        {
            if (sid == ".DEFAULT" || sid.EndsWith("_Classes", StringComparison.Ordinal))
                continue;

            using var userKey = Registry.Users.OpenSubKey($@"{sid}\SOFTWARE\Valve\Steam");
            if (userKey?.GetValue("RunningAppID") is int userAppId && userAppId != 0)
                return userAppId;
        }

        return 0;
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
