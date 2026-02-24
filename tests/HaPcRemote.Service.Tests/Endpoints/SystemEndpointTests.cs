using System.Net;
using System.Net.Http.Json;
using FakeItEasy;
using HaPcRemote.Service.Models;
using Shouldly;

namespace HaPcRemote.Service.Tests.Endpoints;

public class SystemEndpointTests : EndpointTestBase
{
    [Fact]
    public async Task Sleep_ReturnsOk()
    {
        using var client = CreateClient();

        var response = await client.PostAsync("/api/system/sleep", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        A.CallTo(() => PowerService.SleepAsync()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Sleep_ServiceThrows_Returns500()
    {
        A.CallTo(() => PowerService.SleepAsync())
            .Returns(Task.FromException(new InvalidOperationException()));
        using var client = CreateClient();

        var response = await client.PostAsync("/api/system/sleep", null);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Idle_ReturnsSeconds()
    {
        A.CallTo(() => IdleService.GetIdleSeconds()).Returns(42);
        using var client = CreateClient();

        var response = await client.GetAsync("/api/system/idle");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse<int>>(
            AppJsonContext.Default.ApiResponseInt32);
        json.ShouldNotBeNull();
        json.Success.ShouldBeTrue();
        json.Data.ShouldBe(42);
    }

    [Fact]
    public async Task Idle_Unavailable_Returns503()
    {
        A.CallTo(() => IdleService.GetIdleSeconds()).Returns((int?)null);
        using var client = CreateClient();

        var response = await client.GetAsync("/api/system/idle");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Idle_ServiceThrows_Returns500()
    {
        A.CallTo(() => IdleService.GetIdleSeconds())
            .Throws(new InvalidOperationException("no idle service"));
        using var client = CreateClient();

        var response = await client.GetAsync("/api/system/idle");

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }
}
