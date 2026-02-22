using FakeItEasy;
using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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

    private WebApplicationFactory<Program>? _factory;

    protected HttpClient CreateClient(PcRemoteOptions? options = null)
    {
        options ??= new PcRemoteOptions { Auth = new AuthOptions { Enabled = false } };

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the hosted mDNS service so it doesn't try to bind UDP sockets
                    services.RemoveAll<IHostedService>();

                    // Replace real services with fakes
                    services.Replace(ServiceDescriptor.Singleton(CliRunner));
                    services.Replace(ServiceDescriptor.Singleton(AppLauncher));
                    services.Replace(ServiceDescriptor.Singleton(PowerService));

                    // Override options
                    services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<PcRemoteOptions>>(
                        new StaticOptionsMonitor(options)));
                });
            });

        return _factory.CreateClient();
    }

    public void Dispose()
    {
        _factory?.Dispose();
    }

    private sealed class StaticOptionsMonitor(PcRemoteOptions value) : IOptionsMonitor<PcRemoteOptions>
    {
        public PcRemoteOptions CurrentValue => value;
        public PcRemoteOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<PcRemoteOptions, string?> listener) => null;
    }
}
