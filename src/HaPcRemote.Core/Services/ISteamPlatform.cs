namespace HaPcRemote.Service.Services;

public interface ISteamPlatform
{
    string? GetSteamPath();
    string? GetSteamUserId();
    int GetRunningAppId();
    void LaunchSteamUrl(string url);
    void KillProcessesInDirectory(string directory);
}
