using System.Runtime.Versioning;
using FakeItEasy;
using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Models;
using HaPcRemote.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HaPcRemote.Service.Tests.Services;

[SupportedOSPlatform("windows")]
public class AutoSleepServiceTests
{
    private readonly ISteamService _steamService = A.Fake<ISteamService>();
    private readonly IIdleService _idleService = A.Fake<IIdleService>();
    private readonly IPowerService _powerService = A.Fake<IPowerService>();
    private readonly ILogger<AutoSleepService> _logger = A.Fake<ILogger<AutoSleepService>>();

    private AutoSleepService CreateService(int autoSleepAfterMinutes = 30)
    {
        var monitor = A.Fake<IOptionsMonitor<PcRemoteOptions>>();
        A.CallTo(() => monitor.CurrentValue).Returns(new PcRemoteOptions
        {
            Power = new PowerSettings { AutoSleepAfterMinutes = autoSleepAfterMinutes }
        });
        return new AutoSleepService(monitor, _steamService, _idleService, _powerService, _logger);
    }

    [Fact]
    public async Task CheckAndSleepAsync_AutoSleepDisabled_DoesNotSleep()
    {
        var svc = CreateService(autoSleepAfterMinutes: 0);
        A.CallTo(() => _idleService.GetIdleSeconds()).Returns(9999);

        await svc.CheckAndSleepAsync();

        A.CallTo(() => _powerService.SleepAsync()).MustNotHaveHappened();
    }

    [Fact]
    public async Task CheckAndSleepAsync_IdleBelowThreshold_DoesNotSleep()
    {
        var svc = CreateService(autoSleepAfterMinutes: 30);
        A.CallTo(() => _idleService.GetIdleSeconds()).Returns(1700); // 28 min

        await svc.CheckAndSleepAsync();

        A.CallTo(() => _powerService.SleepAsync()).MustNotHaveHappened();
    }

    [Fact]
    public async Task CheckAndSleepAsync_IdleAboveThreshold_NoGame_Sleeps()
    {
        var svc = CreateService(autoSleepAfterMinutes: 30);
        A.CallTo(() => _idleService.GetIdleSeconds()).Returns(1900); // 31 min
        A.CallTo(() => _steamService.GetRunningGameAsync()).Returns(Task.FromResult<SteamRunningGame?>(null));

        await svc.CheckAndSleepAsync();

        A.CallTo(() => _powerService.SleepAsync()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CheckAndSleepAsync_IdleAboveThreshold_GameRunning_DoesNotSleep()
    {
        var svc = CreateService(autoSleepAfterMinutes: 30);
        A.CallTo(() => _idleService.GetIdleSeconds()).Returns(1900);
        A.CallTo(() => _steamService.GetRunningGameAsync())
            .Returns(Task.FromResult<SteamRunningGame?>(new SteamRunningGame { AppId = 730, Name = "Counter-Strike 2" }));

        await svc.CheckAndSleepAsync();

        A.CallTo(() => _powerService.SleepAsync()).MustNotHaveHappened();
    }

    [Fact]
    public async Task CheckAndSleepAsync_IdleNull_DoesNotSleep()
    {
        var svc = CreateService(autoSleepAfterMinutes: 30);
        A.CallTo(() => _idleService.GetIdleSeconds()).Returns((int?)null);

        await svc.CheckAndSleepAsync();

        A.CallTo(() => _powerService.SleepAsync()).MustNotHaveHappened();
    }

    [Fact]
    public async Task CheckAndSleepAsync_LargeIdleButRecentWake_DoesNotSleep()
    {
        // Simulates: PC was asleep a long time → GetLastInputInfo reports huge idle.
        // LastWakeUtc was 5 seconds ago, so effective idle is capped at 5s.
        var svc = CreateService(autoSleepAfterMinutes: 30);
        svc.LastWakeUtc = DateTime.UtcNow.AddSeconds(-5);
        A.CallTo(() => _idleService.GetIdleSeconds()).Returns(9999); // would exceed threshold

        await svc.CheckAndSleepAsync();

        A.CallTo(() => _powerService.SleepAsync()).MustNotHaveHappened();
    }

    [Fact]
    public async Task CheckAndSleepAsync_LargeIdleAndWakeLongAgo_Sleeps()
    {
        // Woke 40 minutes ago → effectiveIdle = min(9999, 2400) = 2400s = 40 min > 30 min threshold.
        var svc = CreateService(autoSleepAfterMinutes: 30);
        svc.LastWakeUtc = DateTime.UtcNow.AddSeconds(-2400); // 40 min ago
        A.CallTo(() => _idleService.GetIdleSeconds()).Returns(9999);
        A.CallTo(() => _steamService.GetRunningGameAsync()).Returns(Task.FromResult<SteamRunningGame?>(null));

        await svc.CheckAndSleepAsync();

        A.CallTo(() => _powerService.SleepAsync()).MustHaveHappenedOnceExactly();
    }
}
