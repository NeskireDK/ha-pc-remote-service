using HaWindowsRemote.Service;
using HaWindowsRemote.Service.Configuration;
using HaWindowsRemote.Service.Endpoints;
using HaWindowsRemote.Service.Middleware;
using HaWindowsRemote.Service.Services;

var builder = WebApplication.CreateBuilder(args);

// Windows Service support
builder.Host.UseWindowsService();

// JSON serialization (AOT-safe)
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));

// Configuration
builder.Services.Configure<PcRemoteOptions>(
    builder.Configuration.GetSection(PcRemoteOptions.SectionName));

var pcRemoteConfig = builder.Configuration
    .GetSection(PcRemoteOptions.SectionName)
    .Get<PcRemoteOptions>() ?? new PcRemoteOptions();

// Generate API key if not configured
if (pcRemoteConfig.Auth.Enabled && string.IsNullOrEmpty(pcRemoteConfig.Auth.ApiKey))
{
    var generatedKey = ApiKeyService.GenerateApiKey();
    var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    ApiKeyService.WriteApiKeyToConfig(configPath, generatedKey);

    // Reload configuration so the new key is picked up
    builder.Configuration.GetSection("PcRemote:Auth:ApiKey").Value = generatedKey;

    Console.WriteLine($"[STARTUP] Generated API key: {generatedKey}");
    Console.WriteLine($"[STARTUP] Key saved to {configPath}");
}

// Configure Kestrel to use configured port
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(pcRemoteConfig.Port);
});

// Application services
builder.Services.AddSingleton<AppService>();
builder.Services.AddSingleton<AudioService>();
builder.Services.AddSingleton<MonitorService>();

// mDNS/Zeroconf advertisement
builder.Services.AddHostedService<MdnsAdvertiserService>();

// Platform-specific services
if (OperatingSystem.IsWindows())
{
    builder.Services.AddSingleton<IPowerService, WindowsPowerService>();
}
else
{
    builder.Services.AddSingleton<IPowerService>(_ =>
        throw new NotSupportedException("Power management is only supported on Windows."));
}

var app = builder.Build();

// Middleware
app.UseMiddleware<ApiKeyMiddleware>();

// Endpoints
app.MapHealthEndpoints();
app.MapSystemEndpoints();
app.MapAppEndpoints();
app.MapAudioEndpoints();
app.MapMonitorEndpoints();

app.Run();
