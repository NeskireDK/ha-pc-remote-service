using HaPcRemote.Service.Models;

namespace HaPcRemote.Service.Services;

public interface IAppService
{
    Task<List<AppInfo>> GetAllStatusesAsync();
    Task LaunchAsync(string appKey);
    Task KillAsync(string appKey);
    Task<AppInfo> GetStatusAsync(string appKey);
}
