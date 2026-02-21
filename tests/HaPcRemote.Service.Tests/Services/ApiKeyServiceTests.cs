using HaPcRemote.Service.Services;
using Shouldly;

namespace HaPcRemote.Service.Tests.Services;

public class ApiKeyServiceTests
{
    [Fact]
    public void GenerateApiKey_Returns32CharString()
    {
        var key = ApiKeyService.GenerateApiKey();

        key.Length.ShouldBe(32);
    }

    [Fact]
    public void GenerateApiKey_ContainsOnlyAlphanumericChars()
    {
        var key = ApiKeyService.GenerateApiKey();

        key.ShouldAllBe(c => char.IsLetterOrDigit(c));
    }

    [Fact]
    public void GenerateApiKey_ProducesUniqueKeys()
    {
        var key1 = ApiKeyService.GenerateApiKey();
        var key2 = ApiKeyService.GenerateApiKey();

        key1.ShouldNotBe(key2);
    }

    [Fact]
    public void WriteApiKeyToConfig_CreatesFileWhenMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test-config-{Guid.NewGuid()}.json");

        try
        {
            ApiKeyService.WriteApiKeyToConfig(path, "test-key-123");

            File.Exists(path).ShouldBeTrue();
            var content = File.ReadAllText(path);
            content.ShouldContain("test-key-123");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteApiKeyToConfig_PreservesExistingSettings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test-config-{Guid.NewGuid()}.json");
        File.WriteAllText(path, """{"PcRemote":{"Port":9999}}""");

        try
        {
            ApiKeyService.WriteApiKeyToConfig(path, "new-key");

            var content = File.ReadAllText(path);
            content.ShouldContain("9999");
            content.ShouldContain("new-key");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
