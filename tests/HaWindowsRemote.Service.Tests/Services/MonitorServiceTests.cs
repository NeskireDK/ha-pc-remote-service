using FakeItEasy;
using HaWindowsRemote.Service.Configuration;
using HaWindowsRemote.Service.Services;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HaWindowsRemote.Service.Tests.Services;

public class MonitorServiceTests : IDisposable
{
    private readonly string _tempDir;

    public MonitorServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"monitor-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private IOptionsMonitor<PcRemoteOptions> CreateOptions(string? profilesPath = null)
    {
        var monitor = A.Fake<IOptionsMonitor<PcRemoteOptions>>();
        A.CallTo(() => monitor.CurrentValue).Returns(new PcRemoteOptions
        {
            ToolsPath = "./tools",
            ProfilesPath = profilesPath ?? _tempDir
        });
        return monitor;
    }

    [Fact]
    public async Task GetProfilesAsync_WithCfgFiles_ReturnsProfileNames()
    {
        File.WriteAllText(Path.Combine(_tempDir, "gaming.cfg"), "");
        File.WriteAllText(Path.Combine(_tempDir, "work.cfg"), "");
        File.WriteAllText(Path.Combine(_tempDir, "readme.txt"), ""); // should be ignored

        var service = new MonitorService(CreateOptions());

        var result = await service.GetProfilesAsync();

        result.Count.ShouldBe(2);
        result.ShouldContain(p => p.Name == "gaming");
        result.ShouldContain(p => p.Name == "work");
    }

    [Fact]
    public async Task GetProfilesAsync_EmptyDirectory_ReturnsEmptyList()
    {
        var service = new MonitorService(CreateOptions());

        var result = await service.GetProfilesAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetProfilesAsync_NonExistentDirectory_ReturnsEmptyList()
    {
        var service = new MonitorService(CreateOptions("/nonexistent/path"));

        var result = await service.GetProfilesAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ApplyProfileAsync_UnknownProfile_ThrowsKeyNotFoundException()
    {
        var service = new MonitorService(CreateOptions());

        await Should.ThrowAsync<KeyNotFoundException>(
            () => service.ApplyProfileAsync("nonexistent"));
    }
}
