using System.Net;
using System.Net.Http.Json;
using FakeItEasy;
using HaPcRemote.Service.Models;
using HaPcRemote.Service.Services;
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

    [Fact]
    public async Task Reload_ReturnsOk_AndCallsService()
    {
        using var client = CreateClient();

        var response = await client.PostAsync("/api/system/reload", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse>(
            AppJsonContext.Default.ApiResponse);
        json.ShouldNotBeNull();
        json.Success.ShouldBeTrue();
        json.Message.ShouldBe("Service reload scheduled");
        A.CallTo(() => RestartService.ScheduleRestart()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Update_UpToDate_ReturnsOk()
    {
        A.CallTo(() => UpdateService.CheckAndApplyAsync(A<CancellationToken>._))
            .Returns(UpdateResult.UpToDate("1.3.4"));
        using var client = CreateClient();

        var response = await client.PostAsync("/api/system/update", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse<UpdateResult>>(
            AppJsonContext.Default.ApiResponseUpdateResult);
        json.ShouldNotBeNull();
        json.Success.ShouldBeTrue();
        json.Data.ShouldNotBeNull();
        json.Data.Status.ShouldBe(UpdateStatus.UpToDate);
        json.Data.CurrentVersion.ShouldBe("1.3.4");
    }

    [Fact]
    public async Task Update_UpdateStarted_ReturnsOk()
    {
        A.CallTo(() => UpdateService.CheckAndApplyAsync(A<CancellationToken>._))
            .Returns(UpdateResult.UpdateStarted("1.3.4", "v1.4.0"));
        using var client = CreateClient();

        var response = await client.PostAsync("/api/system/update", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse<UpdateResult>>(
            AppJsonContext.Default.ApiResponseUpdateResult);
        json.ShouldNotBeNull();
        json.Success.ShouldBeTrue();
        json.Data.ShouldNotBeNull();
        json.Data.Status.ShouldBe(UpdateStatus.UpdateStarted);
        json.Data.CurrentVersion.ShouldBe("1.3.4");
        json.Data.LatestVersion.ShouldBe("v1.4.0");
    }

    [Fact]
    public async Task Update_Failed_Returns500()
    {
        A.CallTo(() => UpdateService.CheckAndApplyAsync(A<CancellationToken>._))
            .Returns(UpdateResult.Failed("Network unavailable"));
        using var client = CreateClient();

        var response = await client.PostAsync("/api/system/update", null);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var json = await response.Content.ReadFromJsonAsync<ApiResponse<UpdateResult>>(
            AppJsonContext.Default.ApiResponseUpdateResult);
        json.ShouldNotBeNull();
        json.Success.ShouldBeFalse();
        json.Data.ShouldNotBeNull();
        json.Data.Status.ShouldBe(UpdateStatus.Failed);
        json.Data.Message.ShouldBe("Network unavailable");
    }

    [Fact]
    public async Task Update_ServiceThrows_Returns500()
    {
        A.CallTo(() => UpdateService.CheckAndApplyAsync(A<CancellationToken>._))
            .Returns(Task.FromException<UpdateResult>(new InvalidOperationException("boom")));
        using var client = CreateClient();

        var response = await client.PostAsync("/api/system/update", null);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }
}
