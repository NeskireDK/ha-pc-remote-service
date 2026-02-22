using System.Net;
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
        A.CallTo(() => PowerService.SleepAsync()).Throws<InvalidOperationException>();
        using var client = CreateClient();

        var response = await client.PostAsync("/api/system/sleep", null);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }
}
