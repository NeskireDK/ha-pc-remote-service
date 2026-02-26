using FakeItEasy;
using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Services;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace HaPcRemote.Service.Tests.Services;

public class SteamAppBootstrapperTests : IDisposable
{
    private readonly ISteamPlatform _platform = A.Fake<ISteamPlatform>();
    private readonly IConfigurationWriter _writer = A.Fake<IConfigurationWriter>();
    private readonly ILogger _logger = A.Fake<ILogger>();
    private readonly string _tempDir;

    public SteamAppBootstrapperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ha-pcremote-bootstrap-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateFakeSteamExe()
    {
        var exePath = Path.Combine(_tempDir, "steam.exe");
        File.WriteAllBytes(exePath, []);
        A.CallTo(() => _platform.GetSteamPath()).Returns(_tempDir);
        return exePath;
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void BootstrapIfNeeded_BothAbsent_WritesBothEntries()
    {
        if (!OperatingSystem.IsWindows()) return;

        var exePath = CreateFakeSteamExe();
        var options = new PcRemoteOptions();

        SteamAppBootstrapper.BootstrapIfNeeded(_platform, _writer, options, _logger);

        A.CallTo(() => _writer.SaveApp("steam", A<AppDefinitionOptions>.That.Matches(a =>
            a.ExePath == exePath &&
            a.Arguments == "" &&
            a.ProcessName == "steam" &&
            a.UseShellExecute == false)))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _writer.SaveApp("steam-bigpicture", A<AppDefinitionOptions>.That.Matches(a =>
            a.ExePath == exePath &&
            a.Arguments == "-bigpicture" &&
            a.ProcessName == "steam" &&
            a.UseShellExecute == false)))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void BootstrapIfNeeded_SteamPresentBigPictureAbsent_WritesOnlyBigPicture()
    {
        if (!OperatingSystem.IsWindows()) return;

        var exePath = CreateFakeSteamExe();
        var options = new PcRemoteOptions
        {
            Apps = new Dictionary<string, AppDefinitionOptions>
            {
                ["steam"] = new() { DisplayName = "Steam", ExePath = exePath, ProcessName = "steam" }
            }
        };

        SteamAppBootstrapper.BootstrapIfNeeded(_platform, _writer, options, _logger);

        A.CallTo(() => _writer.SaveApp("steam", A<AppDefinitionOptions>._))
            .MustNotHaveHappened();

        A.CallTo(() => _writer.SaveApp("steam-bigpicture", A<AppDefinitionOptions>.That.Matches(a =>
            a.ExePath == exePath &&
            a.Arguments == "-bigpicture")))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void BootstrapIfNeeded_BothPresent_DoesNotWrite()
    {
        if (!OperatingSystem.IsWindows()) return;

        var exePath = CreateFakeSteamExe();
        var options = new PcRemoteOptions
        {
            Apps = new Dictionary<string, AppDefinitionOptions>
            {
                ["steam"] = new() { DisplayName = "Steam", ExePath = exePath, ProcessName = "steam" },
                ["steam-bigpicture"] = new() { DisplayName = "Steam Big Picture", ExePath = exePath, ProcessName = "steam" }
            }
        };

        SteamAppBootstrapper.BootstrapIfNeeded(_platform, _writer, options, _logger);

        A.CallTo(() => _writer.SaveApp(A<string>._, A<AppDefinitionOptions>._))
            .MustNotHaveHappened();
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void BootstrapIfNeeded_SteamPathNull_DoesNotWrite()
    {
        if (!OperatingSystem.IsWindows()) return;

        A.CallTo(() => _platform.GetSteamPath()).Returns(null);
        var options = new PcRemoteOptions();

        SteamAppBootstrapper.BootstrapIfNeeded(_platform, _writer, options, _logger);

        A.CallTo(() => _writer.SaveApp(A<string>._, A<AppDefinitionOptions>._))
            .MustNotHaveHappened();
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void BootstrapIfNeeded_SteamExeNotFound_DoesNotWrite()
    {
        if (!OperatingSystem.IsWindows()) return;

        // Point to dir without steam.exe
        A.CallTo(() => _platform.GetSteamPath()).Returns(_tempDir);
        var options = new PcRemoteOptions();

        SteamAppBootstrapper.BootstrapIfNeeded(_platform, _writer, options, _logger);

        A.CallTo(() => _writer.SaveApp(A<string>._, A<AppDefinitionOptions>._))
            .MustNotHaveHappened();
    }
}
