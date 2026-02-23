using FakeItEasy;
using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Endpoints;
using HaPcRemote.Service.Middleware;
using HaPcRemote.Service.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HaPcRemote.Service.Tests.Endpoints;

public class EndpointTestBase : IDisposable
{
    protected readonly ICliRunner CliRunner = A.Fake<ICliRunner>();
    protected readonly IAppLauncher AppLauncher = A.Fake<IAppLauncher>();
    protected readonly IPowerService PowerService = A.Fake<IPowerService>();
    protected readonly ISteamPlatform SteamPlatform = A.Fake<ISteamPlatform>();

    private WebApplication? _app;

    protected HttpClient CreateClient(PcRemoteOptions? options = null)
    {
        options ??= new PcRemoteOptions { Auth = new AuthOptions { Enabled = false } };

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        // JSON serialization
        builder.Services.ConfigureHttpJsonOptions(opts =>
            opts.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));

        // Configuration
        builder.Services.Configure<PcRemoteOptions>(_ => { });
        builder.Services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<PcRemoteOptions>>(
            new StaticOptionsMonitor(options)));

        // Fakes
        builder.Services.AddSingleton(CliRunner);
        builder.Services.AddSingleton(AppLauncher);
        builder.Services.AddSingleton(PowerService);
        builder.Services.AddSingleton(SteamPlatform);

        // Real services that delegate to fakes
        builder.Services.AddSingleton<AppService>();
        builder.Services.AddSingleton<AudioService>();
        builder.Services.AddSingleton<MonitorService>();
        builder.Services.AddSingleton<ModeService>();
        builder.Services.AddSingleton<SteamService>();
        // MdnsAdvertiserService excluded — avoids UDP socket binding in tests

        _app = builder.Build();

        _app.UseMiddleware<ApiKeyMiddleware>();
        _app.MapHealthEndpoints();
        _app.MapSystemEndpoints();
        _app.MapModeEndpoints();
        _app.MapSystemStateEndpoints();
        _app.MapAppEndpoints();
        _app.MapAudioEndpoints();
        _app.MapMonitorEndpoints();
        _app.MapSteamEndpoints();

        _app.StartAsync().GetAwaiter().GetResult();

        return _app.GetTestClient();
    }

    public void Dispose()
    {
        _app?.StopAsync().GetAwaiter().GetResult();
        _app?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    private sealed class StaticOptionsMonitor(PcRemoteOptions value) : IOptionsMonitor<PcRemoteOptions>
    {
        public PcRemoteOptions CurrentValue => value;
        public PcRemoteOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<PcRemoteOptions, string?> listener) => null;
    }
}
