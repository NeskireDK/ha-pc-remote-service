using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HaPcRemote.Tray;

/// <summary>
/// Tray-specific restart implementation that uses <see cref="KestrelRestartService"/>
/// to perform an in-process Kestrel stop + rebuild + start on the same port.
/// </summary>
internal sealed class TrayRestartService(
    KestrelRestartService kestrelRestart,
    IOptionsMonitor<PcRemoteOptions> options,
    ILogger<TrayRestartService> logger) : IRestartService
{
    public void ScheduleRestart()
    {
        var port = options.CurrentValue.Port;
        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            logger.LogInformation("Triggering in-process Kestrel restart on port {Port}", port);
            var restart = kestrelRestart.RestartAsync
                ?? throw new InvalidOperationException("RestartAsync delegate not set");
            await restart(port);
        });
    }
}
