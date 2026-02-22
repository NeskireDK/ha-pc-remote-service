using HaPcRemote.Shared.Ipc;

namespace HaPcRemote.Service.Services;

/// <summary>
/// ISteamPlatform implementation that delegates all Steam operations to the tray app via IPC.
/// The tray runs in the user session and has access to HKCU registry and user-session process launch.
/// </summary>
public sealed class IpcSteamPlatform : ISteamPlatform
{
    private readonly ILogger<IpcSteamPlatform> _logger;

    public IpcSteamPlatform(ILogger<IpcSteamPlatform> logger)
    {
        _logger = logger;
    }

    public string? GetSteamPath()
    {
        var response = Send(new IpcRequest { Type = "steamGetPath" });
        return response?.Stdout;
    }

    public int GetRunningAppId()
    {
        var response = Send(new IpcRequest { Type = "steamGetRunningId" });
        if (response?.Stdout is string s && int.TryParse(s, out var id))
            return id;
        return 0;
    }

    public void LaunchSteamUrl(string url)
    {
        var response = Send(new IpcRequest { Type = "steamLaunchUrl", ProcessArguments = url });
        if (response is not null && !response.Success)
            throw new InvalidOperationException(response.Error ?? "steamLaunchUrl IPC call failed");
    }

    public void KillProcessesInDirectory(string directory)
    {
        var response = Send(new IpcRequest { Type = "steamKillDir", ProcessArguments = directory });
        if (response is not null && !response.Success)
            throw new InvalidOperationException(response.Error ?? "steamKillDir IPC call failed");
    }

    private IpcResponse? Send(IpcRequest request)
    {
        var client = new IpcClient();
        var response = client.SendAsync(request, CancellationToken.None).GetAwaiter().GetResult();
        if (response is null)
            _logger.LogWarning("Tray not connected — Steam IPC call '{Type}' skipped", request.Type);
        return response;
    }
}
