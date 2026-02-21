using FakeItEasy;
using HaWindowsRemote.Service.Configuration;
using HaWindowsRemote.Service.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HaWindowsRemote.Service.Tests.Middleware;

public class ApiKeyMiddlewareTests
{
    private const string ValidApiKey = "test-api-key-12345";

    private readonly ILogger<ApiKeyMiddleware> _logger = A.Fake<ILogger<ApiKeyMiddleware>>();

    private IOptionsMonitor<PcRemoteOptions> CreateOptions(bool enabled = true, string apiKey = ValidApiKey)
    {
        var monitor = A.Fake<IOptionsMonitor<PcRemoteOptions>>();
        A.CallTo(() => monitor.CurrentValue).Returns(new PcRemoteOptions
        {
            Auth = new AuthOptions { Enabled = enabled, ApiKey = apiKey }
        });
        return monitor;
    }

    private static (ApiKeyMiddleware middleware, bool nextCalled) CreateMiddleware(
        ILogger<ApiKeyMiddleware> logger)
    {
        var nextCalled = false;
        var middleware = new ApiKeyMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, logger);
        return (middleware, nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ValidKey_CallsNext()
    {
        var called = false;
        var middleware = new ApiKeyMiddleware(_ => { called = true; return Task.CompletedTask; }, _logger);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/system/sleep";
        context.Request.Headers["X-Api-Key"] = ValidApiKey;

        await middleware.InvokeAsync(context, CreateOptions());

        called.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task InvokeAsync_MissingKey_Returns401()
    {
        var called = false;
        var middleware = new ApiKeyMiddleware(_ => { called = true; return Task.CompletedTask; }, _logger);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/system/sleep";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, CreateOptions());

        called.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(401);
    }

    [Fact]
    public async Task InvokeAsync_WrongKey_Returns401()
    {
        var called = false;
        var middleware = new ApiKeyMiddleware(_ => { called = true; return Task.CompletedTask; }, _logger);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/system/sleep";
        context.Request.Headers["X-Api-Key"] = "wrong-key";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, CreateOptions());

        called.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(401);
    }

    [Fact]
    public async Task InvokeAsync_HealthEndpoint_SkipsAuth()
    {
        var called = false;
        var middleware = new ApiKeyMiddleware(_ => { called = true; return Task.CompletedTask; }, _logger);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/health";

        await middleware.InvokeAsync(context, CreateOptions());

        called.ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AuthDisabled_SkipsAuth()
    {
        var called = false;
        var middleware = new ApiKeyMiddleware(_ => { called = true; return Task.CompletedTask; }, _logger);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/system/sleep";

        await middleware.InvokeAsync(context, CreateOptions(enabled: false));

        called.ShouldBeTrue();
    }
}
