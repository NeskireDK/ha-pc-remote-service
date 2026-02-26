namespace HaPcRemote.Service.Services;

public interface ISteamPlatform
{
    string? GetSteamPath();
    string? GetSteamUserId();
    int GetRunningAppId();
    bool IsSteamRunning();
    void LaunchSteamUrl(string url);
    void KillProcessesInDirectory(string directory);
    IEnumerable<string> GetRunningProcessPaths();
}
