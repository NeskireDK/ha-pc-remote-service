using HaPcRemote.Service.Models;
using ValveKeyValue;

namespace HaPcRemote.Service.Services;

public class SteamService(ISteamPlatform platform)
{
    private List<SteamGame>? _cachedGames;
    private DateTime _cacheExpiry;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public Task<List<SteamGame>> GetGamesAsync()
    {
        if (_cachedGames != null && DateTime.UtcNow < _cacheExpiry)
            return Task.FromResult(_cachedGames);

        var steamPath = platform.GetSteamPath()
            ?? throw new InvalidOperationException("Steam is not installed.");

        var games = LoadInstalledGames(steamPath);
        _cachedGames = games;
        _cacheExpiry = DateTime.UtcNow + CacheDuration;
        return Task.FromResult(games);
    }

    public Task<SteamRunningGame?> GetRunningGameAsync()
    {
        var appId = platform.GetRunningAppId();
        if (appId == 0)
            return Task.FromResult<SteamRunningGame?>(null);

        var name = _cachedGames?.Find(g => g.AppId == appId)?.Name ?? $"Unknown ({appId})";
        return Task.FromResult<SteamRunningGame?>(new SteamRunningGame { AppId = appId, Name = name });
    }

    public async Task LaunchGameAsync(int appId)
    {
        var runningAppId = platform.GetRunningAppId();
        if (runningAppId == appId)
            return; // Already running

        if (runningAppId != 0)
            await StopGameAsync();

        platform.LaunchSteamUrl($"steam://rungameid/{appId}");
    }

    public Task StopGameAsync()
    {
        var appId = platform.GetRunningAppId();
        if (appId == 0)
            return Task.CompletedTask;

        var steamPath = platform.GetSteamPath();
        if (steamPath == null)
            return Task.CompletedTask;

        var installDir = GetGameInstallDir(steamPath, appId);
        if (installDir != null)
            platform.KillProcessesInDirectory(installDir);

        return Task.CompletedTask;
    }

    // ── Static VDF parsers (testable without mocking) ────────────────

    internal static List<string> ParseLibraryFolders(string vdfContent)
    {
        var paths = new List<string>();
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(vdfContent));
        var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        var data = kv.Deserialize(stream);

        foreach (var folder in data)
        {
            var path = folder["path"]?.ToString();
            if (!string.IsNullOrEmpty(path))
                paths.Add(path.Replace(@"\\", @"\"));
        }

        return paths;
    }

    internal static SteamGame? ParseAppManifest(string acfContent)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(acfContent));
        var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        var data = kv.Deserialize(stream);

        var appIdStr = data["appid"]?.ToString();
        var name = data["name"]?.ToString();
        var lastUpdatedStr = data["LastUpdated"]?.ToString();

        if (string.IsNullOrEmpty(appIdStr) || string.IsNullOrEmpty(name))
            return null;

        if (!int.TryParse(appIdStr, out var appId))
            return null;

        long.TryParse(lastUpdatedStr, out var lastUpdated);

        return new SteamGame { AppId = appId, Name = name, LastPlayed = lastUpdated };
    }

    internal static string? ParseInstallDir(string acfContent)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(acfContent));
        var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        var data = kv.Deserialize(stream);
        return data["installdir"]?.ToString();
    }

    private static List<SteamGame> LoadInstalledGames(string steamPath)
    {
        var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersPath))
            return [];

        var vdfContent = File.ReadAllText(libraryFoldersPath);
        var libraryPaths = ParseLibraryFolders(vdfContent);

        var games = new List<SteamGame>();
        foreach (var libPath in libraryPaths)
        {
            var steamAppsDir = Path.Combine(libPath, "steamapps");
            if (!Directory.Exists(steamAppsDir))
                continue;

            foreach (var acfFile in Directory.EnumerateFiles(steamAppsDir, "appmanifest_*.acf"))
            {
                try
                {
                    var content = File.ReadAllText(acfFile);
                    var game = ParseAppManifest(content);
                    if (game != null)
                        games.Add(game);
                }
                catch
                {
                    // Skip corrupt manifests
                }
            }
        }

        return games
            .OrderByDescending(g => g.LastPlayed)
            .Take(20)
            .ToList();
    }

    private static string? GetGameInstallDir(string steamPath, int appId)
    {
        // Search all library folders, not just the main Steam path
        var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersPath))
            return null;

        var vdfContent = File.ReadAllText(libraryFoldersPath);
        var libraryPaths = ParseLibraryFolders(vdfContent);

        foreach (var libPath in libraryPaths)
        {
            var steamAppsDir = Path.Combine(libPath, "steamapps");
            var manifestPath = Path.Combine(steamAppsDir, $"appmanifest_{appId}.acf");

            if (!File.Exists(manifestPath))
                continue;

            var content = File.ReadAllText(manifestPath);
            var installDir = ParseInstallDir(content);

            if (!string.IsNullOrEmpty(installDir))
                return Path.Combine(steamAppsDir, "common", installDir);
        }

        return null;
    }
}
