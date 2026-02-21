using HaWindowsRemote.Service.Configuration;
using HaWindowsRemote.Service.Models;
using Microsoft.Extensions.Options;

namespace HaWindowsRemote.Service.Services;

public class MonitorService
{
    private readonly IOptionsMonitor<PcRemoteOptions> _options;

    public MonitorService(IOptionsMonitor<PcRemoteOptions> options)
    {
        _options = options;
    }

    public Task<List<MonitorProfile>> GetProfilesAsync()
    {
        var profilesPath = _options.CurrentValue.ProfilesPath;

        if (!Directory.Exists(profilesPath))
            return Task.FromResult(new List<MonitorProfile>());

        var profiles = Directory.GetFiles(profilesPath, "*.cfg")
            .Select(f => new MonitorProfile
            {
                Name = Path.GetFileNameWithoutExtension(f)
            })
            .OrderBy(p => p.Name)
            .ToList();

        return Task.FromResult(profiles);
    }

    public async Task ApplyProfileAsync(string profileName)
    {
        var config = _options.CurrentValue;
        var profilePath = Path.Combine(config.ProfilesPath, $"{profileName}.cfg");

        if (!File.Exists(profilePath))
            throw new KeyNotFoundException($"Monitor profile '{profileName}' not found.");

        var toolPath = Path.Combine(config.ToolsPath, "MultiMonitorTool.exe");
        await CliRunner.RunAsync(toolPath, $"/LoadConfig \"{profilePath}\"");
    }
}
