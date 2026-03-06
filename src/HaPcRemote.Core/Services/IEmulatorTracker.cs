namespace HaPcRemote.Service.Services;

public interface IEmulatorTracker
{
    void TrackLaunch(string exePath, int appId, string gameName);
    (int AppId, string Name)? GetLastLaunched(string exePath);
}
