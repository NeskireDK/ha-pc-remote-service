using System.Diagnostics;
using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Models;
using Microsoft.Extensions.Options;

namespace HaPcRemote.Service.Services;

public class AppService
{
    private readonly IOptionsMonitor<PcRemoteOptions> _options;
    private readonly IAppLauncher _appLauncher;

    public AppService(IOptionsMonitor<PcRemoteOptions> options, IAppLauncher appLauncher)
    {
        _options = options;
        _appLauncher = appLauncher;
    }

    public Task<List<AppInfo>> GetAllStatusesAsync()
    {
        var apps = _options.CurrentValue.Apps;
        if (apps is null || apps.Count == 0)
            return Task.FromResult(new List<AppInfo>());

        var runningProcesses = Process.GetProcesses()
            .Select(p => { var name = p.ProcessName; p.Dispose(); return name; })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = apps.Select(kvp => new AppInfo
        {
            Key = kvp.Key,
            DisplayName = kvp.Value.DisplayName,
            IsRunning = runningProcesses.Contains(kvp.Value.ProcessName)
        }).ToList();

        return Task.FromResult(result);
    }

    public async Task LaunchAsync(string appKey)
    {
        var definition = GetDefinition(appKey);
        await _appLauncher.LaunchAsync(definition.ExePath, definition.Arguments);
    }

    public Task KillAsync(string appKey)
    {
        var definition = GetDefinition(appKey);

        var processes = Process.GetProcessesByName(definition.ProcessName);
        foreach (var process in processes)
        {
            process.Kill(entireProcessTree: true);
            process.Dispose();
        }

        return Task.CompletedTask;
    }

    public Task<AppInfo> GetStatusAsync(string appKey)
    {
        var definition = GetDefinition(appKey);

        var info = new AppInfo
        {
            Key = appKey,
            DisplayName = definition.DisplayName,
            IsRunning = IsProcessRunning(definition.ProcessName)
        };

        return Task.FromResult(info);
    }

    private AppDefinitionOptions GetDefinition(string appKey)
    {
        var apps = _options.CurrentValue.Apps;
        if (!apps.TryGetValue(appKey, out var definition))
            throw new KeyNotFoundException($"App '{appKey}' is not configured.");

        return definition;
    }

    private static bool IsProcessRunning(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        var running = processes.Length > 0;
        foreach (var p in processes) p.Dispose();
        return running;
    }
}
