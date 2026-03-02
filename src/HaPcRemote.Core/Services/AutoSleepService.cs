using HaPcRemote.Service.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace HaPcRemote.Service.Services;

/// <summary>
/// Background service that sleeps the PC when idle for the configured duration.
/// Conditions: no Steam game running AND idle time exceeds threshold.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class AutoSleepService(
    IOptionsMonitor<PcRemoteOptions> options,
    ISteamService steamService,
    IIdleService idleService,
    IPowerService powerService,
    ILogger<AutoSleepService> logger) : BackgroundService
{
    internal DateTime LastWakeUtc = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Auto-sleep monitor started");

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        try
        {
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
        finally
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            LastWakeUtc = DateTime.UtcNow;
            logger.LogInformation("System resumed from sleep — resetting wake time");
        }
    }

    internal async Task CheckAndSleepAsync()
    {
        var threshold = options.CurrentValue.Power.AutoSleepAfterMinutes;
        if (threshold <= 0)
            return;

        // Check idle time — cap at seconds-since-wake to avoid false idle after resume
        var idleSeconds = idleService.GetIdleSeconds();
        if (idleSeconds is null)
            return;

        var effectiveIdle = LastWakeUtc == DateTime.MinValue
            ? idleSeconds.Value
            : Math.Min(idleSeconds.Value, (int)(DateTime.UtcNow - LastWakeUtc).TotalSeconds);

        if (effectiveIdle < threshold * 60)
            return;

        // Check no game is running
        var running = await steamService.GetRunningGameAsync();
        if (running is not null)
            return;

        logger.LogInformation(
            "Auto-sleep: idle for {Idle}s (threshold {Threshold}min), no game running — sleeping",
            effectiveIdle, threshold);

        await powerService.SleepAsync();
    }
}
