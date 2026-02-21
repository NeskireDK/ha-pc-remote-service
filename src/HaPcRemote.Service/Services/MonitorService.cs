using System.Text;
using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Models;
using Microsoft.Extensions.Options;

namespace HaPcRemote.Service.Services;

public class MonitorService
{
    private readonly IOptionsMonitor<PcRemoteOptions> _options;

    public MonitorService(IOptionsMonitor<PcRemoteOptions> options)
    {
        _options = options;
    }

    // ── Profile methods ──────────────────────────────────────────────

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

        await CliRunner.RunAsync(GetExePath(), $"/LoadConfig \"{profilePath}\"");
    }

    // ── Monitor control methods ──────────────────────────────────────

    public async Task<List<MonitorInfo>> GetMonitorsAsync()
    {
        var output = await CliRunner.RunAsync(GetExePath(), "/scomma \"\"");
        return ParseCsvOutput(output);
    }

    public async Task EnableMonitorAsync(string id)
    {
        var monitor = await ResolveMonitorAsync(id);
        await CliRunner.RunAsync(GetExePath(), $"/enable \"{monitor.Name}\"");
    }

    public async Task DisableMonitorAsync(string id)
    {
        var monitor = await ResolveMonitorAsync(id);
        await CliRunner.RunAsync(GetExePath(), $"/disable \"{monitor.Name}\"");
    }

    public async Task SetPrimaryAsync(string id)
    {
        var monitor = await ResolveMonitorAsync(id);
        await CliRunner.RunAsync(GetExePath(), $"/SetPrimary \"{monitor.Name}\"");
    }

    public async Task SoloMonitorAsync(string id)
    {
        var monitors = await GetMonitorsAsync();
        var target = FindMonitor(monitors, id);

        // Disable all monitors except the target
        foreach (var m in monitors.Where(m => !MatchesId(m, id)))
        {
            if (m.IsActive)
                await CliRunner.RunAsync(GetExePath(), $"/disable \"{m.Name}\"");
        }

        // Enable target and set as primary
        if (!target.IsActive)
            await CliRunner.RunAsync(GetExePath(), $"/enable \"{target.Name}\"");

        await CliRunner.RunAsync(GetExePath(), $"/SetPrimary \"{target.Name}\"");
    }

    // ── CSV parsing ──────────────────────────────────────────────────

    internal static List<MonitorInfo> ParseCsvOutput(string csvOutput)
    {
        var monitors = new List<MonitorInfo>();

        foreach (var line in csvOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var columns = SplitCsvLine(trimmed);
            if (columns.Count < 14)
                continue;

            // Column mapping (0-indexed):
            // 0=Name, 1=Short Monitor ID, 2=Monitor ID, 3=Monitor Key,
            // 4=Monitor String, 5=Monitor Name, 6=Serial Number,
            // 7-8=scale info, 9=Orientation, 10=Width, 11=Height,
            // 12=BitsPerPixel, 13=DisplayFrequency

            // Filter out disconnected monitors — Name will be empty or state indicates disconnected
            var name = columns[0];
            if (string.IsNullOrWhiteSpace(name))
                continue;

            // Check for "Disconnected" in the Monitor Key or other status indicators
            if (columns[3].Contains("Disconnected", StringComparison.OrdinalIgnoreCase))
                continue;

            var shortMonitorId = columns[1];
            var monitorName = columns[5];
            var serialNumber = columns[6];

            int.TryParse(columns[10], out var width);
            int.TryParse(columns[11], out var height);
            int.TryParse(columns[13], out var displayFrequency);

            // Active = has a valid width and height (connected + enabled)
            var isActive = width > 0 && height > 0;

            // Primary detection: check if any column indicates primary status.
            // MultiMonitorTool typically has orientation at col 9.
            // We check columns beyond 13 if available for additional flags.
            var isPrimary = false;
            for (var i = 14; i < columns.Count; i++)
            {
                if (string.Equals(columns[i].Trim(), "Yes", StringComparison.OrdinalIgnoreCase))
                {
                    isPrimary = true;
                    break;
                }
            }

            monitors.Add(new MonitorInfo
            {
                Name = name,
                MonitorId = shortMonitorId,
                SerialNumber = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber,
                MonitorName = monitorName,
                Width = width,
                Height = height,
                DisplayFrequency = displayFrequency,
                IsActive = isActive,
                IsPrimary = isPrimary
            });
        }

        return monitors;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private string GetExePath() =>
        Path.Combine(_options.CurrentValue.ToolsPath, "MultiMonitorTool.exe");

    private async Task<MonitorInfo> ResolveMonitorAsync(string id)
    {
        var monitors = await GetMonitorsAsync();
        return FindMonitor(monitors, id);
    }

    internal static MonitorInfo FindMonitor(List<MonitorInfo> monitors, string id)
    {
        return monitors.Find(m => MatchesId(m, id))
            ?? throw new KeyNotFoundException($"Monitor '{id}' not found.");
    }

    private static bool MatchesId(MonitorInfo m, string id) =>
        string.Equals(m.Name, id, StringComparison.OrdinalIgnoreCase)
        || string.Equals(m.MonitorId, id, StringComparison.OrdinalIgnoreCase)
        || (!string.IsNullOrEmpty(m.SerialNumber)
            && string.Equals(m.SerialNumber, id, StringComparison.OrdinalIgnoreCase));

    private static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
