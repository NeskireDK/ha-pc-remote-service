using System.Diagnostics;
using HaWindowsRemote.Service.Configuration;
using HaWindowsRemote.Service.Models;
using Microsoft.Extensions.Options;

namespace HaWindowsRemote.Service.Services;

public class AppService
{
    private readonly IOptionsMonitor<PcRemoteOptions> _options;

    public AppService(IOptionsMonitor<PcRemoteOptions> options)
    {
        _options = options;
    }

    public Task<List<AppInfo>> GetAllStatusesAsync()
    {
        var apps = _options.CurrentValue.Apps;
        var result = apps.Select(kvp => new AppInfo
        {
            Key = kvp.Key,
            DisplayName = kvp.Value.DisplayName,
            IsRunning = IsProcessRunning(kvp.Value.ProcessName)
        }).ToList();

        return Task.FromResult(result);
    }

    public Task LaunchAsync(string appKey)
    {
        var definition = GetDefinition(appKey);

        var startInfo = new ProcessStartInfo
        {
            FileName = definition.ExePath,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrEmpty(definition.Arguments))
            startInfo.Arguments = definition.Arguments;

        Process.Start(startInfo);
        return Task.CompletedTask;
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
