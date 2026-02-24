using HaPcRemote.Service.Models;

namespace HaPcRemote.Service.Services;

public interface ISteamService
{
    Task<List<SteamGame>> GetGamesAsync();
    Task<SteamRunningGame?> GetRunningGameAsync();
    Task<SteamRunningGame?> LaunchGameAsync(int appId);
    Task StopGameAsync();
}
