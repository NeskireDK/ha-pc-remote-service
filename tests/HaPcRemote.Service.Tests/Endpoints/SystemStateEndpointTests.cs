using System.Net;
using System.Net.Http.Json;
using FakeItEasy;
using HaPcRemote.Service.Models;
using Shouldly;

namespace HaPcRemote.Service.Tests.Endpoints;

public class SystemStateEndpointTests : EndpointTestBase
{
    private const string SampleCsv = "Device,Speakers,Render,Render,50.0%";

    [Fact]
    public async Task GetState_ReturnsOk()
    {
        A.CallTo(() => CliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        A.CallTo(() => SteamPlatform.GetRunningAppId()).Returns(0);
        A.CallTo(() => SteamPlatform.GetSteamPath()).Returns((string?)null);
        using var client = CreateClient();

        var response = await client.GetAsync("/api/system/state");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse<SystemState>>(
            AppJsonContext.Default.ApiResponseSystemState);
        json.ShouldNotBeNull();
        json.Success.ShouldBeTrue();
        json.Data.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetState_AudioAvailable_PopulatesAudioField()
    {
        A.CallTo(() => CliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        A.CallTo(() => SteamPlatform.GetRunningAppId()).Returns(0);
        A.CallTo(() => SteamPlatform.GetSteamPath()).Returns((string?)null);
        using var client = CreateClient();

        var response = await client.GetAsync("/api/system/state");

        var json = await response.Content.ReadFromJsonAsync<ApiResponse<SystemState>>(
            AppJsonContext.Default.ApiResponseSystemState);
        json!.Data!.Audio.ShouldNotBeNull();
        json.Data.Audio.Current.ShouldBe("Speakers");
        json.Data.Audio.Volume.ShouldBe(50);
    }

    [Fact]
    public async Task GetState_AudioFails_ReturnsNullAudio()
    {
        A.CallTo(() => CliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Throws(new InvalidOperationException("tool missing"));
        A.CallTo(() => SteamPlatform.GetRunningAppId()).Returns(0);
        A.CallTo(() => SteamPlatform.GetSteamPath()).Returns((string?)null);
        using var client = CreateClient();

        var response = await client.GetAsync("/api/system/state");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse<SystemState>>(
            AppJsonContext.Default.ApiResponseSystemState);
        json!.Success.ShouldBeTrue();
        json.Data!.Audio.ShouldBeNull();
    }

    [Fact]
    public async Task GetState_NoRunningGame_RunningGameIsNull()
    {
        A.CallTo(() => CliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        A.CallTo(() => SteamPlatform.GetRunningAppId()).Returns(0);
        A.CallTo(() => SteamPlatform.GetSteamPath()).Returns((string?)null);
        using var client = CreateClient();

        var response = await client.GetAsync("/api/system/state");

        var json = await response.Content.ReadFromJsonAsync<ApiResponse<SystemState>>(
            AppJsonContext.Default.ApiResponseSystemState);
        json!.Data!.RunningGame.ShouldBeNull();
    }
}
