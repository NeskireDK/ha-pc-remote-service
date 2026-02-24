using System.Net;
using System.Net.Http.Json;
using FakeItEasy;
using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Models;
using Shouldly;

namespace HaPcRemote.Service.Tests.Endpoints;

public class ModeEndpointTests : EndpointTestBase
{
    private static PcRemoteOptions OptionsWithModes() => new()
    {
        Auth = new AuthOptions { Enabled = false },
        Modes = new Dictionary<string, ModeConfig>
        {
            ["gaming"] = new() { Volume = 80 },
            ["work"] = new() { Volume = 40 }
        }
    };

    [Fact]
    public async Task GetModes_ReturnsModeNames()
    {
        using var client = CreateClient(OptionsWithModes());

        var response = await client.GetAsync("/api/system/modes");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<string>>>(
            AppJsonContext.Default.ApiResponseIReadOnlyListString);
        json.ShouldNotBeNull();
        json.Success.ShouldBeTrue();
        json.Data!.Count.ShouldBe(2);
        json.Data.ShouldContain("gaming");
        json.Data.ShouldContain("work");
    }

    [Fact]
    public async Task GetModes_NoModesConfigured_ReturnsEmptyList()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/system/modes");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<string>>>(
            AppJsonContext.Default.ApiResponseIReadOnlyListString);
        json.ShouldNotBeNull();
        json.Data!.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ApplyMode_UnknownMode_Returns404()
    {
        using var client = CreateClient(OptionsWithModes());

        var response = await client.PostAsync("/api/system/mode/nonexistent", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse>(
            AppJsonContext.Default.ApiResponse);
        json.ShouldNotBeNull();
        json.Success.ShouldBeFalse();
        json.Message!.ShouldContain("nonexistent");
    }

    [Fact]
    public async Task ApplyMode_VolumeOnly_ReturnsOk()
    {
        // AudioService is IAudioService fake — SetVolumeAsync returns completed task by default
        using var client = CreateClient(OptionsWithModes());

        var response = await client.PostAsync("/api/system/mode/gaming", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse>(
            AppJsonContext.Default.ApiResponse);
        json.ShouldNotBeNull();
        json.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ApplyMode_ResponseMessageContainsModeKey()
    {
        using var client = CreateClient(OptionsWithModes());

        var response = await client.PostAsync("/api/system/mode/work", null);

        var json = await response.Content.ReadFromJsonAsync<ApiResponse>(
            AppJsonContext.Default.ApiResponse);
        json.ShouldNotBeNull();
        json.Message!.ShouldContain("work");
    }

    [Fact]
    public async Task GetModes_NoModesConfigured_ReturnsSuccessTrue()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/system/modes");

        var json = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<string>>>(
            AppJsonContext.Default.ApiResponseIReadOnlyListString);
        json.ShouldNotBeNull();
        json.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ApplyMode_EmptyModeName_Returns404()
    {
        // Route with empty segment won't match — ASP.NET returns 404
        using var client = CreateClient(OptionsWithModes());

        var response = await client.PostAsync("/api/system/mode/", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ApplyMode_ModeWithAllNullConfig_ReturnsOk()
    {
        // A mode with no volume/device/profile — ApplyModeAsync is a no-op
        var options = new PcRemoteOptions
        {
            Auth = new AuthOptions { Enabled = false },
            Modes = new Dictionary<string, ModeConfig>
            {
                ["idle"] = new() // all properties null/default
            }
        };
        using var client = CreateClient(options);

        var response = await client.PostAsync("/api/system/mode/idle", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
