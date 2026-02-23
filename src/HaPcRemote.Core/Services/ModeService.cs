using HaPcRemote.Service.Configuration;
using Microsoft.Extensions.Options;

namespace HaPcRemote.Service.Services;

public class ModeService(
    IOptionsMonitor<PcRemoteOptions> options,
    AudioService audioService,
    MonitorService monitorService,
    AppService appService)
{
    public IReadOnlyList<string> GetModeNames() =>
        options.CurrentValue.Modes.Keys.ToList();

    public async Task ApplyModeAsync(string modeName)
    {
        var modes = options.CurrentValue.Modes;
        if (!modes.TryGetValue(modeName, out var config))
            throw new KeyNotFoundException($"Mode '{modeName}' not found.");

        if (config.AudioDevice is not null)
            await audioService.SetDefaultDeviceAsync(config.AudioDevice);

        if (config.MonitorProfile is not null)
            await monitorService.ApplyProfileAsync(config.MonitorProfile);

        if (config.Volume.HasValue)
            await audioService.SetVolumeAsync(config.Volume.Value);

        if (config.KillApp is not null)
            await appService.KillAsync(config.KillApp);

        if (config.LaunchApp is not null)
            await appService.LaunchAsync(config.LaunchApp);
    }
}
