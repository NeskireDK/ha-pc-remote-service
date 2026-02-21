using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Models;
using Microsoft.Extensions.Options;

namespace HaPcRemote.Service.Services;

public class AudioService
{
    private readonly IOptionsMonitor<PcRemoteOptions> _options;
    private readonly ICliRunner _cliRunner;

    public AudioService(IOptionsMonitor<PcRemoteOptions> options, ICliRunner cliRunner)
    {
        _options = options;
        _cliRunner = cliRunner;
    }

    public async Task<List<AudioDevice>> GetDevicesAsync()
    {
        var output = await _cliRunner.RunAsync(GetExePath(), ["/scomma", "", "/Columns", "Name,Direction,Default,Volume Percent"]);
        return ParseCsvOutput(output);
    }

    public async Task<AudioDevice?> GetCurrentDeviceAsync()
    {
        var devices = await GetDevicesAsync();
        return devices.Find(d => d.IsDefault);
    }

    public async Task SetDefaultDeviceAsync(string deviceName)
    {
        await _cliRunner.RunAsync(GetExePath(), ["/SetDefault", deviceName, "1"]);
    }

    public async Task SetVolumeAsync(int level)
    {
        var current = await GetCurrentDeviceAsync()
            ?? throw new InvalidOperationException("No default audio device found.");

        await _cliRunner.RunAsync(GetExePath(), ["/SetVolume", current.Name, level.ToString()]);
        await _cliRunner.RunAsync(GetExePath(), ["/Unmute", current.Name]);
    }

    private string GetExePath() =>
        Path.Combine(_options.CurrentValue.ToolsPath, "SoundVolumeView.exe");

    internal static List<AudioDevice> ParseCsvOutput(string csvOutput)
    {
        var devices = new List<AudioDevice>();
        foreach (var line in csvOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var columns = CliRunner.SplitCsvLine(trimmed);
            if (columns.Count < 4)
                continue;

            // Columns selected via /Columns flag:
            // [0] Name, [1] Direction, [2] Default (Console), [3] Volume Percent

            if (!string.Equals(columns[1], "Render", StringComparison.OrdinalIgnoreCase))
                continue;

            devices.Add(new AudioDevice
            {
                Name = columns[0],
                IsDefault = string.Equals(columns[2], "Render", StringComparison.OrdinalIgnoreCase),
                Volume = ParseVolumePercent(columns[3])
            });
        }
        return devices;
    }

    private static int ParseVolumePercent(string value)
    {
        // Value comes as "50.0%" — strip the % and parse
        var cleaned = value.Trim().TrimEnd('%');
        return double.TryParse(cleaned, System.Globalization.CultureInfo.InvariantCulture, out var d)
            ? (int)Math.Round(d)
            : 0;
    }

}
