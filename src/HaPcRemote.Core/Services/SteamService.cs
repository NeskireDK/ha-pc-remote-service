using HaPcRemote.Service.Models;
using ValveKeyValue;

namespace HaPcRemote.Service.Services;

public class SteamService(ISteamPlatform platform) : ISteamService
{
    private List<SteamGame>? _cachedGames;
    private DateTime _cacheExpiry;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    public async Task<List<SteamGame>> GetGamesAsync()
    {
        if (_cachedGames != null && DateTime.UtcNow < _cacheExpiry)
            return _cachedGames;

        var steamPath = platform.GetSteamPath();
        if (steamPath == null)
        {
            if (_cachedGames != null)
                return _cachedGames;

            throw new InvalidOperationException("Steam is not installed.");
        }

        var games = await Task.Run(() => LoadInstalledGames(steamPath));
        _cachedGames = games;
        _cacheExpiry = DateTime.UtcNow + CacheDuration;
        return games;
    }

    public async Task<SteamRunningGame?> GetRunningGameAsync()
    {
        var appId = platform.GetRunningAppId();
        if (appId == 0)
            return null;

        // Warm the cache if not yet populated
        if (_cachedGames == null || _cachedGames.Count == 0)
        {
            try { await GetGamesAsync(); }
            catch (InvalidOperationException) { /* Steam path unavailable, continue without cache */ }
        }

        var name = _cachedGames?.Find(g => g.AppId == appId)?.Name;

        // Game is running but not in the top-20 list — look it up from its manifest directly
        if (name == null)
        {
            var steamPath = platform.GetSteamPath();
            if (steamPath != null)
                name = FindGameNameFromManifest(steamPath, appId);
        }

        name ??= $"Unknown ({appId})";
        return new SteamRunningGame { AppId = appId, Name = name };
    }

    public async Task<SteamRunningGame?> LaunchGameAsync(int appId)
    {
        var runningAppId = platform.GetRunningAppId();
        if (runningAppId == appId)
            return await GetRunningGameAsync();

        if (runningAppId != 0)
            await StopGameAsync();

        platform.LaunchSteamUrl($"steam://rungameid/{appId}");

        // Brief poll — Steam registers running state within seconds
        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(500);
            var running = platform.GetRunningAppId();
            if (running == appId)
                return await GetRunningGameAsync();
        }

        return null; // Steam didn't accept the launch
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
        var lastPlayedStr = data["LastPlayed"]?.ToString();
        var lastUpdatedStr = data["LastUpdated"]?.ToString();

        if (string.IsNullOrEmpty(appIdStr) || string.IsNullOrEmpty(name))
            return null;

        if (!int.TryParse(appIdStr, out var appId))
            return null;

        // Prefer LastPlayed (actual play history); fall back to LastUpdated (install/update time)
        if (!long.TryParse(lastPlayedStr, out var lastPlayed))
            long.TryParse(lastUpdatedStr, out lastPlayed);

        return new SteamGame { AppId = appId, Name = name, LastPlayed = lastPlayed };
    }

    internal static string? ParseInstallDir(string acfContent)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(acfContent));
        var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        var data = kv.Deserialize(stream);
        return data["installdir"]?.ToString();
    }

    private static string? FindGameNameFromManifest(string steamPath, int appId)
    {
        var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersPath))
            return null;

        var vdfContent = File.ReadAllText(libraryFoldersPath);
        var libraryPaths = ParseLibraryFolders(vdfContent);

        foreach (var libPath in libraryPaths)
        {
            var manifestPath = Path.Combine(libPath, "steamapps", $"appmanifest_{appId}.acf");
            if (!File.Exists(manifestPath))
                continue;

            try
            {
                var content = File.ReadAllText(manifestPath);
                var game = ParseAppManifest(content);
                if (game != null)
                    return game.Name;
            }
            catch
            {
                // Skip corrupt manifest
            }
        }

        return null;
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
