using FakeItEasy;
using HaPcRemote.Service.Services;
using Shouldly;

namespace HaPcRemote.Service.Tests.Services;

public class SteamServiceTests
{
    private readonly ISteamPlatform _platform = A.Fake<ISteamPlatform>();

    private SteamService CreateService() => new(_platform);

    // ── ParseLibraryFolders tests (static) ───────────────────────────

    [Fact]
    public void ParseLibraryFolders_ValidVdf_ReturnsLibraryPaths()
    {
        var paths = SteamService.ParseLibraryFolders(TestData.Load("library-folders.vdf"));

        paths.Count.ShouldBe(2);
        paths[0].ShouldBe(@"C:\Program Files (x86)\Steam");
        paths[1].ShouldBe(@"D:\SteamLibrary");
    }

    [Fact]
    public void ParseLibraryFolders_EmptyVdf_ReturnsEmptyList()
    {
        var vdf = """
            "libraryfolders"
            {
            }
            """;

        var paths = SteamService.ParseLibraryFolders(vdf);

        paths.ShouldBeEmpty();
    }

    // ── ParseAppManifest tests (static) ──────────────────────────────

    [Fact]
    public void ParseAppManifest_ValidAcf_ReturnsGameInfo()
    {
        var game = SteamService.ParseAppManifest(TestData.Load("app-manifest-730.acf"));

        game.ShouldNotBeNull();
        game.AppId.ShouldBe(730);
        game.Name.ShouldBe("Counter-Strike 2");
        game.LastPlayed.ShouldBe(1700000000L);
    }

    [Fact]
    public void ParseAppManifest_MissingName_ReturnsNull()
    {
        var acf = """
            "AppState"
            {
                "appid"     "730"
                "StateFlags"    "4"
            }
            """;

        var game = SteamService.ParseAppManifest(acf);

        game.ShouldBeNull();
    }

    [Fact]
    public void ParseAppManifest_MissingAppId_ReturnsNull()
    {
        var acf = """
            "AppState"
            {
                "name"      "Counter-Strike 2"
                "StateFlags"    "4"
            }
            """;

        var game = SteamService.ParseAppManifest(acf);

        game.ShouldBeNull();
    }

    [Fact]
    public void ParseAppManifest_InvalidAppId_ReturnsNull()
    {
        var acf = """
            "AppState"
            {
                "appid"     "not_a_number"
                "name"      "Counter-Strike 2"
            }
            """;

        var game = SteamService.ParseAppManifest(acf);

        game.ShouldBeNull();
    }

    [Fact]
    public void ParseAppManifest_MissingLastUpdated_DefaultsToZero()
    {
        var acf = """
            "AppState"
            {
                "appid"     "730"
                "name"      "Counter-Strike 2"
            }
            """;

        var game = SteamService.ParseAppManifest(acf);

        game.ShouldNotBeNull();
        game.LastPlayed.ShouldBe(0L);
    }

    // ── ParseInstallDir tests (static) ───────────────────────────────

    [Fact]
    public void ParseInstallDir_ValidAcf_ReturnsInstallDir()
    {
        var installDir = SteamService.ParseInstallDir(TestData.Load("app-manifest-730.acf"));

        installDir.ShouldBe("Counter-Strike Global Offensive");
    }

    [Fact]
    public void ParseInstallDir_MissingInstallDir_ReturnsNull()
    {
        var acf = """
            "AppState"
            {
                "appid"     "730"
                "name"      "Counter-Strike 2"
            }
            """;

        var installDir = SteamService.ParseInstallDir(acf);

        installDir.ShouldBeNull();
    }

    // ── GetRunningGameAsync tests ────────────────────────────────────

    [Fact]
    public async Task GetRunningGame_NoGame_ReturnsNull()
    {
        A.CallTo(() => _platform.GetRunningAppId()).Returns(0);
        var service = CreateService();

        var result = await service.GetRunningGameAsync();

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetRunningGame_GameRunning_ReturnsGameInfo()
    {
        A.CallTo(() => _platform.GetRunningAppId()).Returns(730);
        var service = CreateService();

        var result = await service.GetRunningGameAsync();

        result.ShouldNotBeNull();
        result.AppId.ShouldBe(730);
    }

    // ── LaunchGameAsync tests ────────────────────────────────────────

    [Fact]
    public async Task LaunchGame_SameGameRunning_NoOp()
    {
        A.CallTo(() => _platform.GetRunningAppId()).Returns(730);
        var service = CreateService();

        await service.LaunchGameAsync(730);

        A.CallTo(() => _platform.LaunchSteamUrl(A<string>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task LaunchGame_NoGameRunning_LaunchesSteamUrl()
    {
        A.CallTo(() => _platform.GetRunningAppId()).Returns(0);
        var service = CreateService();

        await service.LaunchGameAsync(730);

        A.CallTo(() => _platform.LaunchSteamUrl("steam://rungameid/730"))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task LaunchGame_DifferentGameRunning_StopsFirst()
    {
        // First call: different game is running (for LaunchGameAsync check)
        // Second call: still running (for StopGameAsync check)
        // Third call: from StopGameAsync's GetSteamPath check
        A.CallTo(() => _platform.GetRunningAppId()).Returns(570);
        A.CallTo(() => _platform.GetSteamPath()).Returns((string?)null);
        var service = CreateService();

        await service.LaunchGameAsync(730);

        // StopGameAsync was called (checks RunningAppId and GetSteamPath)
        A.CallTo(() => _platform.GetSteamPath()).MustHaveHappened();
        A.CallTo(() => _platform.LaunchSteamUrl("steam://rungameid/730"))
            .MustHaveHappenedOnceExactly();
    }

    // ── StopGameAsync tests ──────────────────────────────────────────

    [Fact]
    public async Task StopGame_NoGameRunning_NoOp()
    {
        A.CallTo(() => _platform.GetRunningAppId()).Returns(0);
        var service = CreateService();

        await service.StopGameAsync();

        A.CallTo(() => _platform.KillProcessesInDirectory(A<string>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task StopGame_NoSteamPath_NoOp()
    {
        A.CallTo(() => _platform.GetRunningAppId()).Returns(730);
        A.CallTo(() => _platform.GetSteamPath()).Returns((string?)null);
        var service = CreateService();

        await service.StopGameAsync();

        A.CallTo(() => _platform.KillProcessesInDirectory(A<string>._)).MustNotHaveHappened();
    }
}
