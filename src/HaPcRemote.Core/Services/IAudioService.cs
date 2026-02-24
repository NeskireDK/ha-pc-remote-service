using HaPcRemote.Service.Models;

namespace HaPcRemote.Service.Services;

public interface IAudioService
{
    Task<List<AudioDevice>> GetDevicesAsync();
    Task<AudioDevice?> GetCurrentDeviceAsync();
    Task SetDefaultDeviceAsync(string deviceName);
    Task SetVolumeAsync(int level);
}
