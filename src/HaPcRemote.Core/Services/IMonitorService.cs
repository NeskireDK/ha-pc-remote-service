using HaPcRemote.Service.Models;

namespace HaPcRemote.Service.Services;

public interface IMonitorService
{
    Task<List<MonitorInfo>> GetMonitorsAsync();
    Task<List<MonitorProfile>> GetProfilesAsync();
    Task ApplyProfileAsync(string profileName);
    Task EnableMonitorAsync(string id);
    Task DisableMonitorAsync(string id);
    Task SetPrimaryAsync(string id);
    Task SoloMonitorAsync(string id);
}
