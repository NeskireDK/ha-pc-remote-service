using System.Net;
using HaPcRemote.Service.Configuration;
using Shouldly;

namespace HaPcRemote.Service.Tests.Endpoints;

public class AuthEndpointTests : EndpointTestBase
{
    [Fact]
    public async Task AuthEnabled_MissingKey_Returns401()
    {
        using var client = CreateClient(new PcRemoteOptions
        {
            Auth = new AuthOptions { Enabled = true, ApiKey = "secret" }
        });

        var response = await client.PostAsync("/api/system/sleep", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthEnabled_ValidKey_Returns200()
    {
        using var client = CreateClient(new PcRemoteOptions
        {
            Auth = new AuthOptions { Enabled = true, ApiKey = "secret" }
        });
        client.DefaultRequestHeaders.Add("X-Api-Key", "secret");

        var response = await client.PostAsync("/api/system/sleep", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuthEnabled_WrongKey_Returns401()
    {
        using var client = CreateClient(new PcRemoteOptions
        {
            Auth = new AuthOptions { Enabled = true, ApiKey = "secret" }
        });
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong");

        var response = await client.PostAsync("/api/system/sleep", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
