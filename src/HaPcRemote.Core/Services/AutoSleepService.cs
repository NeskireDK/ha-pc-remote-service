using HaPcRemote.Service.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HaPcRemote.Service.Services;

/// <summary>
/// Background service that sleeps the PC when idle for the configured duration.
/// Conditions: no Steam game running AND idle time exceeds threshold.
/// </summary>
public sealed class AutoSleepService(
    IOptionsMonitor<PcRemoteOptions> options,
    ISteamService steamService,
    IIdleService idleService,
    IPowerService powerService,
    ILogger<AutoSleepService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Auto-sleep monitor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
                await CheckAndSleepAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Auto-sleep check error");
            }
        }
    }

    private async Task CheckAndSleepAsync()
    {
        var threshold = options.CurrentValue.Power.AutoSleepAfterMinutes;
        if (threshold <= 0)
            return;

        // Check idle time
        var idleSeconds = idleService.GetIdleSeconds();
        if (idleSeconds is null || idleSeconds.Value < threshold * 60)
            return;

        // Check no game is running
        var running = await steamService.GetRunningGameAsync();
        if (running is not null)
            return;

        logger.LogInformation(
            "Auto-sleep: idle for {Idle}s (threshold {Threshold}min), no game running — sleeping",
            idleSeconds.Value, threshold);

        await powerService.SleepAsync();
    }
}
