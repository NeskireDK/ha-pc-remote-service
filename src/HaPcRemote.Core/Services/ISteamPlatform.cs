namespace HaPcRemote.Service.Services;

public interface ISteamPlatform
{
    string? GetSteamPath();
    int GetRunningAppId();
    void LaunchSteamUrl(string url);
    void KillProcessesInDirectory(string directory);
}
