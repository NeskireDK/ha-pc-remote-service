using FakeItEasy;
using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HaPcRemote.Service.Tests.Middleware;

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

    [Fact]
    public async Task InvokeAsync_EmptyApiKey_Returns401()
    {
        var called = false;
        var middleware = new ApiKeyMiddleware(_ => { called = true; return Task.CompletedTask; }, _logger);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/system/sleep";
        context.Request.Headers["X-Api-Key"] = "";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, CreateOptions());

        called.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(401);
    }

    [Fact]
    public async Task InvokeAsync_WhitespaceApiKey_Returns401()
    {
        var called = false;
        var middleware = new ApiKeyMiddleware(_ => { called = true; return Task.CompletedTask; }, _logger);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/system/sleep";
        context.Request.Headers["X-Api-Key"] = "   ";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, CreateOptions());

        called.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(401);
    }

    [Fact]
    public async Task InvokeAsync_CorrectKeyWithLeadingWhitespace_Returns401()
    {
        // Key with whitespace is not equal to the stored key — must be exact match
        var called = false;
        var middleware = new ApiKeyMiddleware(_ => { called = true; return Task.CompletedTask; }, _logger);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/system/sleep";
        context.Request.Headers["X-Api-Key"] = " " + ValidApiKey;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, CreateOptions());

        called.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(401);
    }

    [Fact]
    public async Task InvokeAsync_AuthEndpointCaseInsensitive_SkipsAuth()
    {
        // /api/health path check is case-insensitive
        var called = false;
        var middleware = new ApiKeyMiddleware(_ => { called = true; return Task.CompletedTask; }, _logger);
        var context = new DefaultHttpContext();
        context.Request.Path = "/API/HEALTH";

        await middleware.InvokeAsync(context, CreateOptions());

        called.ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ValidKey_ResponseStatusRemainsDefault()
    {
        var middleware = new ApiKeyMiddleware(_ => Task.CompletedTask, _logger);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/system/sleep";
        context.Request.Headers["X-Api-Key"] = ValidApiKey;

        await middleware.InvokeAsync(context, CreateOptions());

        // Default status code is 200 — middleware should not change it
        context.Response.StatusCode.ShouldBe(200);
    }
}
