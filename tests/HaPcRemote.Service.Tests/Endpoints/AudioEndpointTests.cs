using System.Net;
using System.Net.Http.Json;
using FakeItEasy;
using HaPcRemote.Service.Models;
using Shouldly;

namespace HaPcRemote.Service.Tests.Endpoints;

public class AudioEndpointTests : EndpointTestBase
{
    private const string SampleCsv = "Speakers,Render,Render,50.0%\nHeadphones,Render,,75.0%";

    [Fact]
    public async Task GetDevices_ReturnsDeviceList()
    {
        A.CallTo(() => CliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        using var client = CreateClient();

        var response = await client.GetAsync("/api/audio/devices");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse<List<AudioDevice>>>(
            AppJsonContext.Default.ApiResponseListAudioDevice);
        json.ShouldNotBeNull();
        json.Success.ShouldBeTrue();
        json.Data!.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetCurrent_ReturnsDefaultDevice()
    {
        A.CallTo(() => CliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        using var client = CreateClient();

        var response = await client.GetAsync("/api/audio/current");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse<AudioDevice>>(
            AppJsonContext.Default.ApiResponseAudioDevice);
        json.ShouldNotBeNull();
        json.Data!.Name.ShouldBe("Speakers");
    }

    [Fact]
    public async Task GetCurrent_NoDefault_Returns404()
    {
        A.CallTo(() => CliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns("Speakers,Render,,50.0%"); // No default
        using var client = CreateClient();

        var response = await client.GetAsync("/api/audio/current");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetVolume_ValidRange_ReturnsOk()
    {
        A.CallTo(() => CliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        using var client = CreateClient();

        var response = await client.PostAsync("/api/audio/volume/50", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task SetVolume_OutOfRange_Returns400(int level)
    {
        using var client = CreateClient();

        var response = await client.PostAsync($"/api/audio/volume/{level}", null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse>(
            AppJsonContext.Default.ApiResponse);
        json.ShouldNotBeNull();
        json.Success.ShouldBeFalse();
        json.Message.ShouldContain("between 0 and 100");
    }

    [Fact]
    public async Task SetDefault_ReturnsOk()
    {
        using var client = CreateClient();

        var response = await client.PostAsync("/api/audio/set/Headphones", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        A.CallTo(() => CliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .MustHaveHappenedOnceExactly();
    }
}
