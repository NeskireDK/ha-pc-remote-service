using HaPcRemote.Service;
using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Endpoints;
using HaPcRemote.Service.Logging;
using HaPcRemote.Shared.Configuration;
using HaPcRemote.Service.Middleware;
using HaPcRemote.Service.Models;
using HaPcRemote.Service.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Windows Service support
builder.Host.UseWindowsService();

// File logging — visible in tray log viewer
builder.Logging.AddProvider(new FileLoggerProvider(ConfigPaths.GetLogFilePath()));

// Writable config: use %ProgramData%\HaPcRemote so that runtime-generated
// settings (API key) persist even when the exe is in a read-only location
// like C:\Program Files.
var writableConfigDir = ConfigPaths.GetWritableConfigDir();
var writableConfigPath = ConfigPaths.GetWritableConfigPath();
builder.Configuration.AddJsonFile(writableConfigPath, optional: true, reloadOnChange: true);

// JSON serialization (AOT-safe)
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));

// Configuration
builder.Services.Configure<PcRemoteOptions>(
    builder.Configuration.GetSection(PcRemoteOptions.SectionName));

// Resolve relative paths against the exe directory, not CWD.
// Windows Services start with CWD=C:\Windows\System32, so relative paths
// like ./tools/ must be anchored to AppContext.BaseDirectory instead.
builder.Services.PostConfigure<PcRemoteOptions>(options =>
{
    var baseDir = AppContext.BaseDirectory;

    if (!Path.IsPathRooted(options.ToolsPath))
        options.ToolsPath = Path.GetFullPath(options.ToolsPath, baseDir);

    if (!Path.IsPathRooted(options.ProfilesPath))
        options.ProfilesPath = Path.GetFullPath(options.ProfilesPath, baseDir);

    foreach (var app in options.Apps.Values)
    {
        if (!string.IsNullOrEmpty(app.ExePath) && !Path.IsPathRooted(app.ExePath))
            app.ExePath = Path.GetFullPath(app.ExePath, baseDir);
    }
});

var pcRemoteConfig = builder.Configuration
    .GetSection(PcRemoteOptions.SectionName)
    .Get<PcRemoteOptions>() ?? new PcRemoteOptions();

// Generate API key if not configured
if (pcRemoteConfig.Auth.Enabled && string.IsNullOrEmpty(pcRemoteConfig.Auth.ApiKey))
{
    var generatedKey = ApiKeyService.GenerateApiKey();

    // Write to the writable config directory; fall back to exe directory
    // if ProgramData is not available (e.g. portable/dev scenarios).
    string configPath;
    try
    {
        Directory.CreateDirectory(writableConfigDir);
        configPath = writableConfigPath;
    }
    catch
    {
        configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

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

// Application services — delegate CLI and app launch to tray app (user session)
builder.Services.AddSingleton<ICliRunner, TrayCliRunner>();
builder.Services.AddSingleton<IAppLauncher, TrayAppLauncher>();
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

// Steam operations are delegated to the tray app via IPC (works on any platform)
builder.Services.AddSingleton<ISteamPlatform, IpcSteamPlatform>();
builder.Services.AddSingleton<SteamService>();

var app = builder.Build();

// Startup diagnostics — log resolved paths and what was found
{
    var startupLogger = app.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");
    var resolvedOptions = app.Services.GetRequiredService<IOptions<PcRemoteOptions>>().Value;

    var toolsExists = Directory.Exists(resolvedOptions.ToolsPath);
    startupLogger.LogInformation("[Startup] ToolsPath: {Path} ({Status})",
        resolvedOptions.ToolsPath, toolsExists ? "exists" : "NOT FOUND");

    var profilesExists = Directory.Exists(resolvedOptions.ProfilesPath);
    var profileCount = profilesExists
        ? Directory.GetFiles(resolvedOptions.ProfilesPath, "*.cfg").Length
        : 0;
    var profilesStatus = profilesExists ? $"exists, {profileCount} profiles" : "NOT FOUND";
    startupLogger.LogInformation("[Startup] ProfilesPath: {Path} ({Status})",
        resolvedOptions.ProfilesPath, profilesStatus);

    startupLogger.LogInformation("[Startup] LogFile: {Path}",
        ConfigPaths.GetLogFilePath());

    startupLogger.LogInformation("[Startup] Config: {Path}",
        ConfigPaths.GetWritableConfigPath());
}

// Global exception handler
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception");
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(
                ApiResponse.Fail("Internal server error"),
                AppJsonContext.Default.ApiResponse);
        }
    }
});

// Middleware
app.UseMiddleware<ApiKeyMiddleware>();

// Endpoints
app.MapHealthEndpoints();
app.MapSystemEndpoints();
app.MapAppEndpoints();
app.MapAudioEndpoints();
app.MapMonitorEndpoints();
app.MapSteamEndpoints();

app.Run();

// Make the implicit Program class accessible for WebApplicationFactory in tests
public partial class Program { }
