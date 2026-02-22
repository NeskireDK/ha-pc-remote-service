using FakeItEasy;
using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Services;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HaPcRemote.Service.Tests.Services;

public class AppServiceTests
{
    private static IOptionsMonitor<PcRemoteOptions> CreateOptions(
        Dictionary<string, AppDefinitionOptions>? apps = null)
    {
        var monitor = A.Fake<IOptionsMonitor<PcRemoteOptions>>();
        A.CallTo(() => monitor.CurrentValue).Returns(new PcRemoteOptions
        {
            Apps = apps ?? new Dictionary<string, AppDefinitionOptions>()
        });
        return monitor;
    }

    [Fact]
    public async Task LaunchAsync_UnknownKey_ThrowsKeyNotFoundException()
    {
        var service = new AppService(CreateOptions(), A.Fake<IAppLauncher>());

        await Should.ThrowAsync<KeyNotFoundException>(
            () => service.LaunchAsync("nonexistent"));
    }

    [Fact]
    public async Task KillAsync_UnknownKey_ThrowsKeyNotFoundException()
    {
        var service = new AppService(CreateOptions(), A.Fake<IAppLauncher>());

        await Should.ThrowAsync<KeyNotFoundException>(
            () => service.KillAsync("nonexistent"));
    }

    [Fact]
    public async Task GetStatusAsync_UnknownKey_ThrowsKeyNotFoundException()
    {
        var service = new AppService(CreateOptions(), A.Fake<IAppLauncher>());

        await Should.ThrowAsync<KeyNotFoundException>(
            () => service.GetStatusAsync("nonexistent"));
    }

    [Fact]
    public async Task GetAllStatusesAsync_ReturnsConfiguredApps()
    {
        var apps = new Dictionary<string, AppDefinitionOptions>
        {
            ["notepad"] = new()
            {
                DisplayName = "Notepad",
                ExePath = "notepad.exe",
                ProcessName = "notepad_test_unlikely_running_12345"
            },
            ["calc"] = new()
            {
                DisplayName = "Calculator",
                ExePath = "calc.exe",
                ProcessName = "calc_test_unlikely_running_12345"
            }
        };
        var service = new AppService(CreateOptions(apps), A.Fake<IAppLauncher>());

        var result = await service.GetAllStatusesAsync();

        result.Count.ShouldBe(2);
        result.ShouldContain(a => a.Key == "notepad" && a.DisplayName == "Notepad");
        result.ShouldContain(a => a.Key == "calc" && a.DisplayName == "Calculator");
    }

    [Fact]
    public async Task GetAllStatusesAsync_EmptyConfig_ReturnsEmptyList()
    {
        var service = new AppService(CreateOptions(), A.Fake<IAppLauncher>());

        var result = await service.GetAllStatusesAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetStatusAsync_KnownKey_ReturnsAppInfo()
    {
        var apps = new Dictionary<string, AppDefinitionOptions>
        {
            ["myapp"] = new()
            {
                DisplayName = "My App",
                ExePath = "myapp.exe",
                ProcessName = "myapp_test_unlikely_running_12345"
            }
        };
        var service = new AppService(CreateOptions(apps), A.Fake<IAppLauncher>());

        var result = await service.GetStatusAsync("myapp");

        result.Key.ShouldBe("myapp");
        result.DisplayName.ShouldBe("My App");
        result.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task LaunchAsync_KnownKey_CallsAppLauncherWithCorrectArgs()
    {
        var launcher = A.Fake<IAppLauncher>();
        var apps = new Dictionary<string, AppDefinitionOptions>
        {
            ["notepad"] = new()
            {
                DisplayName = "Notepad",
                ExePath = @"C:\Windows\notepad.exe",
                Arguments = "--new-window",
                ProcessName = "notepad"
            }
        };
        var service = new AppService(CreateOptions(apps), launcher);

        await service.LaunchAsync("notepad");

        A.CallTo(() => launcher.LaunchAsync(@"C:\Windows\notepad.exe", "--new-window"))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task LaunchAsync_NullArguments_PassesNullToLauncher()
    {
        var launcher = A.Fake<IAppLauncher>();
        var apps = new Dictionary<string, AppDefinitionOptions>
        {
            ["calc"] = new()
            {
                DisplayName = "Calculator",
                ExePath = "calc.exe",
                ProcessName = "calc"
            }
        };
        var service = new AppService(CreateOptions(apps), launcher);

        await service.LaunchAsync("calc");

        A.CallTo(() => launcher.LaunchAsync("calc.exe", null))
            .MustHaveHappenedOnceExactly();
    }
}
