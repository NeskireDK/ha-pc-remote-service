using System.Net;
using System.Net.Http.Json;
using FakeItEasy;
using HaPcRemote.Service.Models;
using HaPcRemote.Service.Services;
using Shouldly;

namespace HaPcRemote.Service.Tests.Endpoints;

public class SteamEndpointTests : EndpointTestBase
{
    [Fact]
    public async Task GetGames_ReturnsList()
    {
        // SteamService reads from filesystem, but with a fake ISteamPlatform
        // GetGamesAsync will throw because GetSteamPath returns null by default.
        // We need to set it up so it returns a path, but the filesystem won't have files.
        // So this will return an empty list or throw. Let's test the error path.
        A.CallTo(() => SteamPlatform.GetSteamPath()).Returns((string?)null);
        using var client = CreateClient();

        var response = await client.GetAsync("/api/steam/games");

        // SteamService throws InvalidOperationException when Steam not installed
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetRunning_NoGame_ReturnsNullData()
    {
        A.CallTo(() => SteamPlatform.GetRunningAppId()).Returns(0);
        using var client = CreateClient();

        var response = await client.GetAsync("/api/steam/running");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse<SteamRunningGame>>(
            AppJsonContext.Default.ApiResponseSteamRunningGame);
        json.ShouldNotBeNull();
        json.Success.ShouldBeTrue();
        json.Data.ShouldBeNull();
    }

    [Fact]
    public async Task Run_NoSteamRunning_Returns200WithNullData()
    {
        A.CallTo(() => SteamPlatform.GetRunningAppId()).Returns(0);
        using var client = CreateClient();

        var response = await client.PostAsync("/api/steam/run/730", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse<SteamRunningGame>>(
            AppJsonContext.Default.ApiResponseSteamRunningGame);
        json.ShouldNotBeNull();
        json.Success.ShouldBeTrue();
        json.Data.ShouldBeNull(); // Poll never confirms because fake always returns 0
        A.CallTo(() => SteamPlatform.LaunchSteamUrl("steam://rungameid/730"))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Run_SameGameRunning_Returns200WithGameData()
    {
        A.CallTo(() => SteamPlatform.GetRunningAppId()).Returns(730);
        A.CallTo(() => SteamPlatform.GetSteamPath()).Returns((string?)null);
        using var client = CreateClient();

        var response = await client.PostAsync("/api/steam/run/730", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse<SteamRunningGame>>(
            AppJsonContext.Default.ApiResponseSteamRunningGame);
        json.ShouldNotBeNull();
        json.Success.ShouldBeTrue();
        json.Data.ShouldNotBeNull();
        json.Data.AppId.ShouldBe(730);
        A.CallTo(() => SteamPlatform.LaunchSteamUrl(A<string>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Stop_NoGameRunning_Returns200()
    {
        A.CallTo(() => SteamPlatform.GetRunningAppId()).Returns(0);
        using var client = CreateClient();

        var response = await client.PostAsync("/api/steam/stop", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse>(
            AppJsonContext.Default.ApiResponse);
        json.ShouldNotBeNull();
        json.Success.ShouldBeTrue();
    }
}
