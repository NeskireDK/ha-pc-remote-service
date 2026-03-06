using HaPcRemote.Service.Services;
using Shouldly;

namespace HaPcRemote.Service.Tests.Services;

public class SteamUserIdResolverTests
{
    // ── Steam64 to Steam3 conversion via ParseMostRecentUser ──────────

    [Fact]
    public void ParseMostRecentUser_ValidMostRecent_ReturnsSteam3Id()
    {
        var vdf = """
            "users"
            {
                "76561198012345678"
                {
                    "AccountName"   "testuser"
                    "MostRecent"    "1"
                }
            }
            """;

        var result = SteamUserIdResolver.ParseMostRecentUser(vdf);

        // 76561198012345678 - 76561197960265728 = 52079950
        result.ShouldBe("52079950");
    }

    [Fact]
    public void ParseMostRecentUser_LowercaseMostRecent_ReturnsSteam3Id()
    {
        var vdf = """
            "users"
            {
                "76561198012345678"
                {
                    "AccountName"   "testuser"
                    "mostrecent"    "1"
                }
            }
            """;

        var result = SteamUserIdResolver.ParseMostRecentUser(vdf);

        result.ShouldBe("52079950");
    }

    [Fact]
    public void ParseMostRecentUser_MultiplUsers_ReturnsMostRecent()
    {
        var vdf = """
            "users"
            {
                "76561198000000001"
                {
                    "AccountName"   "user1"
                    "MostRecent"    "0"
                }
                "76561198000000002"
                {
                    "AccountName"   "user2"
                    "MostRecent"    "1"
                }
            }
            """;

        var result = SteamUserIdResolver.ParseMostRecentUser(vdf);

        // 76561198000000002 - 76561197960265728 = 39734274
        result.ShouldBe("39734274");
    }

    [Fact]
    public void ParseMostRecentUser_NoMostRecentFlag_FallsBackToFirstUser()
    {
        var vdf = """
            "users"
            {
                "76561198012345678"
                {
                    "AccountName"   "testuser"
                }
            }
            """;

        var result = SteamUserIdResolver.ParseMostRecentUser(vdf);

        result.ShouldBe("52079950");
    }

    [Fact]
    public void ParseMostRecentUser_MostRecentZero_FallsBackToFirstUser()
    {
        var vdf = """
            "users"
            {
                "76561198012345678"
                {
                    "AccountName"   "testuser"
                    "MostRecent"    "0"
                }
            }
            """;

        var result = SteamUserIdResolver.ParseMostRecentUser(vdf);

        // No MostRecent=1, falls back to first user
        result.ShouldBe("52079950");
    }

    [Fact]
    public void ParseMostRecentUser_NoUsers_ReturnsNull()
    {
        var vdf = """
            "users"
            {
            }
            """;

        var result = SteamUserIdResolver.ParseMostRecentUser(vdf);

        result.ShouldBeNull();
    }

    [Fact]
    public void ParseMostRecentUser_NonNumericSteam64_ReturnsNull()
    {
        var vdf = """
            "users"
            {
                "not_a_number"
                {
                    "AccountName"   "testuser"
                    "MostRecent"    "1"
                }
            }
            """;

        var result = SteamUserIdResolver.ParseMostRecentUser(vdf);

        result.ShouldBeNull();
    }

    [Fact]
    public void ParseMostRecentUser_Steam64BelowOffset_ReturnsNull()
    {
        // steam64 < offset => steam3 <= 0 => null
        var vdf = """
            "users"
            {
                "100"
                {
                    "AccountName"   "testuser"
                    "MostRecent"    "1"
                }
            }
            """;

        var result = SteamUserIdResolver.ParseMostRecentUser(vdf);

        result.ShouldBeNull();
    }

    [Fact]
    public void ParseMostRecentUser_Steam64ExactlyAtOffset_ReturnsNull()
    {
        // steam3 = 0, which is <= 0
        var vdf = """
            "users"
            {
                "76561197960265728"
                {
                    "AccountName"   "testuser"
                    "MostRecent"    "1"
                }
            }
            """;

        var result = SteamUserIdResolver.ParseMostRecentUser(vdf);

        result.ShouldBeNull();
    }

    [Fact]
    public void ParseMostRecentUser_Steam64OneAboveOffset_ReturnsOne()
    {
        var vdf = """
            "users"
            {
                "76561197960265729"
                {
                    "AccountName"   "testuser"
                    "MostRecent"    "1"
                }
            }
            """;

        var result = SteamUserIdResolver.ParseMostRecentUser(vdf);

        result.ShouldBe("1");
    }

    // ── Resolve with directory fallback ───────────────────────────────

    [Fact]
    public void Resolve_NonExistentPath_ReturnsNull()
    {
        var result = SteamUserIdResolver.Resolve(@"C:\NonExistent_SteamPath_12345");

        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_ValidLoginUsersVdf_ReturnsSteam3Id()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"steam-test-{Guid.NewGuid():N}");
        var configDir = Path.Combine(tempDir, "config");
        Directory.CreateDirectory(configDir);

        try
        {
            var vdf = """
                "users"
                {
                    "76561198012345678"
                    {
                        "AccountName"   "testuser"
                        "MostRecent"    "1"
                    }
                }
                """;
            File.WriteAllText(Path.Combine(configDir, "loginusers.vdf"), vdf);

            var result = SteamUserIdResolver.Resolve(tempDir);

            result.ShouldBe("52079950");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Resolve_NoLoginUsersVdf_FallsBackToUserdataDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"steam-test-{Guid.NewGuid():N}");
        var userDataDir = Path.Combine(tempDir, "userdata", "12345");
        Directory.CreateDirectory(userDataDir);

        try
        {
            var result = SteamUserIdResolver.Resolve(tempDir);

            result.ShouldBe("12345");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Resolve_UserdataSkipsZeroDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"steam-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempDir, "userdata", "0"));
        Directory.CreateDirectory(Path.Combine(tempDir, "userdata", "67890"));

        try
        {
            var result = SteamUserIdResolver.Resolve(tempDir);

            result.ShouldBe("67890");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Resolve_UserdataSkipsNonNumericDirectories()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"steam-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempDir, "userdata", "anonymous"));
        Directory.CreateDirectory(Path.Combine(tempDir, "userdata", "99999"));

        try
        {
            var result = SteamUserIdResolver.Resolve(tempDir);

            result.ShouldBe("99999");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Resolve_UserdataOnlyZeroAndNonNumeric_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"steam-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempDir, "userdata", "0"));
        Directory.CreateDirectory(Path.Combine(tempDir, "userdata", "temp"));

        try
        {
            var result = SteamUserIdResolver.Resolve(tempDir);

            result.ShouldBeNull();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Resolve_EmptyUserdataDirectory_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"steam-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempDir, "userdata"));

        try
        {
            var result = SteamUserIdResolver.Resolve(tempDir);

            result.ShouldBeNull();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Resolve_MalformedLoginUsersVdf_FallsBackToUserdata()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"steam-test-{Guid.NewGuid():N}");
        var configDir = Path.Combine(tempDir, "config");
        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "userdata", "11111"));

        try
        {
            // Write garbage that ValveKeyValue can't parse
            File.WriteAllText(Path.Combine(configDir, "loginusers.vdf"), "{{{{garbage}}}}");

            var result = SteamUserIdResolver.Resolve(tempDir);

            result.ShouldBe("11111");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
