using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Models;
using Microsoft.Extensions.Options;

namespace HaPcRemote.Service.Services;

public class AudioService
{
    private readonly IOptionsMonitor<PcRemoteOptions> _options;

    public AudioService(IOptionsMonitor<PcRemoteOptions> options)
    {
        _options = options;
    }

    public async Task<List<AudioDevice>> GetDevicesAsync()
    {
        var output = await CliRunner.RunAsync(GetExePath(), "/scomma \"\"");
        return ParseCsvOutput(output);
    }

    public async Task<AudioDevice?> GetCurrentDeviceAsync()
    {
        var devices = await GetDevicesAsync();
        return devices.Find(d => d.IsDefault);
    }

    public async Task SetDefaultDeviceAsync(string deviceName)
    {
        await CliRunner.RunAsync(GetExePath(), $"/SetDefault \"{deviceName}\" 1");
    }

    public async Task SetVolumeAsync(int level)
    {
        var current = await GetCurrentDeviceAsync()
            ?? throw new InvalidOperationException("No default audio device found.");

        await CliRunner.RunAsync(GetExePath(), $"/SetVolume \"{current.Name}\" {level}");
        await CliRunner.RunAsync(GetExePath(), $"/Unmute \"{current.Name}\"");
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
            if (columns.Count < 8)
                continue;

            // Column 3 = Direction (Render/Capture), only keep Render
            if (!string.Equals(columns[3], "Render", StringComparison.OrdinalIgnoreCase))
                continue;

            var name = columns[0];
            var isDefault = string.Equals(columns[5], "Render", StringComparison.OrdinalIgnoreCase);
            var volume = ParseVolumePercent(columns[7]);

            devices.Add(new AudioDevice
            {
                Name = name,
                Volume = volume,
                IsDefault = isDefault
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
