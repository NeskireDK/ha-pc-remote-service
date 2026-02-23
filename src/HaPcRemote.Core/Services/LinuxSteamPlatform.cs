using System.Diagnostics;
using System.Runtime.Versioning;
using ValveKeyValue;

namespace HaPcRemote.Service.Services;

[SupportedOSPlatform("linux")]
public sealed class LinuxSteamPlatform : ISteamPlatform
{
    private static readonly string[] KnownSteamPaths =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".steam", "steam"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Steam"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".var", "app",
            "com.valvesoftware.Steam", "data", "Steam"),
    ];

    public string? GetSteamPath()
    {
        foreach (var path in KnownSteamPaths)
        {
            if (Directory.Exists(path))
                return path;
        }
        return null;
    }

    public int GetRunningAppId()
    {
        var steamPath = GetSteamPath();
        if (steamPath is null) return 0;

        // Steam writes RunningAppID to registry.vdf when a game is running
        var registryVdf = Path.Combine(steamPath, "registry.vdf");
        if (!File.Exists(registryVdf)) return 0;

        try
        {
            using var stream = File.OpenRead(registryVdf);
            var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            var root = kv.Deserialize(stream);

            // Path: Registry/HKCU/Software/Valve/Steam/RunningAppID
            var steamNode = FindChild(FindChild(FindChild(FindChild(root, "HKCU"), "Software"), "Valve"), "Steam");
            if (steamNode is null) return 0;

            var value = steamNode["RunningAppID"]?.ToString();
            if (value is not null && int.TryParse(value, out var appId))
                return appId;
        }
        catch { }

        return 0;
    }

    private static KVObject? FindChild(KVObject? parent, string name)
    {
        if (parent is null) return null;
        foreach (var child in parent)
        {
            if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                return child;
        }
        return null;
    }

    public void LaunchSteamUrl(string url)
    {
        // UseShellExecute on Linux delegates to xdg-open, which handles steam:// URIs
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
