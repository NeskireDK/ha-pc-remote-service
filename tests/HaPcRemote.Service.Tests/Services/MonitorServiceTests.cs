using FakeItEasy;
using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Models;
using HaPcRemote.Service.Services;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HaPcRemote.Service.Tests.Services;

public class MonitorServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ICliRunner _cliRunner = A.Fake<ICliRunner>();

    // Realistic MultiMonitorTool /scomma output (14+ columns per line):
    // 0=Name, 1=Short Monitor ID, 2=Monitor ID, 3=Monitor Key, 4=Monitor String,
    // 5=Monitor Name, 6=Serial Number, 7=Adapter Name, 8=Resolution,
    // 9=Orientation, 10=Width, 11=Height, 12=BitsPerPixel, 13=DisplayFrequency,
    // 14+=additional flags (Primary=Yes/No)
    private const string SampleCsv =
        """
        \\.\DISPLAY1,GSM59A4,GSM59A4-1234,Active,\\.\DISPLAY1\GSM59A4,LG ULTRAGEAR,ABC123,NVIDIA GeForce,3840x2160,0,3840,2160,32,144,Yes
        \\.\DISPLAY2,DEL4321,DEL4321-5678,Active,\\.\DISPLAY2\DEL4321,Dell U2723QE,XYZ789,NVIDIA GeForce,2560x1440,0,2560,1440,32,60,No
        \\.\DISPLAY3,SAM0F00,SAM0F00-9012,Active,\\.\DISPLAY3\SAM0F00,Samsung Odyssey,,NVIDIA GeForce,1920x1080,0,1920,1080,32,240,No
        """;

    private const string CsvWithDisconnected =
        """
        \\.\DISPLAY1,GSM59A4,GSM59A4-1234,Active,\\.\DISPLAY1\GSM59A4,LG ULTRAGEAR,ABC123,NVIDIA GeForce,3840x2160,0,3840,2160,32,144,Yes
        \\.\DISPLAY4,AOC1234,AOC1234-0000,Disconnected,\\.\DISPLAY4\AOC1234,AOC Monitor,DISC01,NVIDIA GeForce,,0,0,0,0,0,No
        """;

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

    private MonitorService CreateService(string? profilesPath = null) =>
        new MonitorService(CreateOptions(profilesPath), _cliRunner);

    // ── Profile tests ─────────────────────────────────────────────────

    [Fact]
    public async Task GetProfilesAsync_WithCfgFiles_ReturnsProfileNames()
    {
        File.WriteAllText(Path.Combine(_tempDir, "gaming.cfg"), "");
        File.WriteAllText(Path.Combine(_tempDir, "work.cfg"), "");
        File.WriteAllText(Path.Combine(_tempDir, "readme.txt"), ""); // should be ignored

        var service = CreateService();

        var result = await service.GetProfilesAsync();

        result.Count.ShouldBe(2);
        result.ShouldContain(p => p.Name == "gaming");
        result.ShouldContain(p => p.Name == "work");
    }

    [Fact]
    public async Task GetProfilesAsync_EmptyDirectory_ReturnsEmptyList()
    {
        var service = CreateService();

        var result = await service.GetProfilesAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetProfilesAsync_NonExistentDirectory_ReturnsEmptyList()
    {
        var service = CreateService("/nonexistent/path");

        var result = await service.GetProfilesAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ApplyProfileAsync_UnknownProfile_ThrowsKeyNotFoundException()
    {
        var service = CreateService();

        await Should.ThrowAsync<KeyNotFoundException>(
            () => service.ApplyProfileAsync("nonexistent"));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData("sub/dir")]
    [InlineData("sub\\dir")]
    [InlineData("..")]
    public async Task ApplyProfileAsync_PathTraversal_ThrowsArgumentException(string profileName)
    {
        var service = CreateService();

        await Should.ThrowAsync<ArgumentException>(
            () => service.ApplyProfileAsync(profileName));
    }

    [Fact]
    public async Task ApplyProfileAsync_ValidProfile_CallsCliRunner()
    {
        File.WriteAllText(Path.Combine(_tempDir, "gaming.cfg"), "");
        var service = CreateService();

        await service.ApplyProfileAsync("gaming");

        A.CallTo(() => _cliRunner.RunAsync(
            A<string>.That.EndsWith("MultiMonitorTool.exe"),
            A<IEnumerable<string>>.That.Contains("/LoadConfig"),
            A<int>._)).MustHaveHappenedOnceExactly();
    }

    // ── CSV parsing tests ─────────────────────────────────────────────

    [Fact]
    public void ParseCsvOutput_ParsesAllConnectedMonitors()
    {
        var monitors = MonitorService.ParseCsvOutput(SampleCsv);

        monitors.Count.ShouldBe(3);
    }

    [Fact]
    public void ParseCsvOutput_ParsesDisplayName()
    {
        var monitors = MonitorService.ParseCsvOutput(SampleCsv);

        monitors[0].Name.ShouldBe(@"\\.\DISPLAY1");
        monitors[1].Name.ShouldBe(@"\\.\DISPLAY2");
    }

    [Fact]
    public void ParseCsvOutput_ParsesShortMonitorId()
    {
        var monitors = MonitorService.ParseCsvOutput(SampleCsv);

        monitors[0].MonitorId.ShouldBe("GSM59A4");
        monitors[1].MonitorId.ShouldBe("DEL4321");
    }

    [Fact]
    public void ParseCsvOutput_ParsesFriendlyName()
    {
        var monitors = MonitorService.ParseCsvOutput(SampleCsv);

        monitors[0].MonitorName.ShouldBe("LG ULTRAGEAR");
        monitors[1].MonitorName.ShouldBe("Dell U2723QE");
    }

    [Fact]
    public void ParseCsvOutput_ParsesSerialNumber()
    {
        var monitors = MonitorService.ParseCsvOutput(SampleCsv);

        monitors[0].SerialNumber.ShouldBe("ABC123");
        monitors[1].SerialNumber.ShouldBe("XYZ789");
    }

    [Fact]
    public void ParseCsvOutput_EmptySerialNumber_ReturnsNull()
    {
        var monitors = MonitorService.ParseCsvOutput(SampleCsv);

        monitors[2].SerialNumber.ShouldBeNull();
    }

    [Fact]
    public void ParseCsvOutput_ParsesResolution()
    {
        var monitors = MonitorService.ParseCsvOutput(SampleCsv);

        monitors[0].Width.ShouldBe(3840);
        monitors[0].Height.ShouldBe(2160);
        monitors[1].Width.ShouldBe(2560);
        monitors[1].Height.ShouldBe(1440);
    }

    [Fact]
    public void ParseCsvOutput_ParsesDisplayFrequency()
    {
        var monitors = MonitorService.ParseCsvOutput(SampleCsv);

        monitors[0].DisplayFrequency.ShouldBe(144);
        monitors[1].DisplayFrequency.ShouldBe(60);
        monitors[2].DisplayFrequency.ShouldBe(240);
    }

    [Fact]
    public void ParseCsvOutput_IdentifiesPrimaryMonitor()
    {
        var monitors = MonitorService.ParseCsvOutput(SampleCsv);

        monitors[0].IsPrimary.ShouldBeTrue();
        monitors[1].IsPrimary.ShouldBeFalse();
        monitors[2].IsPrimary.ShouldBeFalse();
    }

    [Fact]
    public void ParseCsvOutput_ActiveMonitors_HavePositiveResolution()
    {
        var monitors = MonitorService.ParseCsvOutput(SampleCsv);

        monitors.ShouldAllBe(m => m.IsActive);
    }

    [Fact]
    public void ParseCsvOutput_FiltersDisconnectedMonitors()
    {
        var monitors = MonitorService.ParseCsvOutput(CsvWithDisconnected);

        monitors.Count.ShouldBe(1);
        monitors[0].Name.ShouldBe(@"\\.\DISPLAY1");
    }

    [Fact]
    public void ParseCsvOutput_EmptyInput_ReturnsEmptyList()
    {
        var monitors = MonitorService.ParseCsvOutput("");

        monitors.ShouldBeEmpty();
    }

    [Fact]
    public void ParseCsvOutput_WhitespaceOnlyInput_ReturnsEmptyList()
    {
        var monitors = MonitorService.ParseCsvOutput("   \n  \n  ");

        monitors.ShouldBeEmpty();
    }

    [Fact]
    public void ParseCsvOutput_LineTooShort_IsSkipped()
    {
        var csv = @"\\.\DISPLAY1,GSM59A4,GSM59A4-1234";
        var monitors = MonitorService.ParseCsvOutput(csv);

        monitors.ShouldBeEmpty();
    }

    [Fact]
    public void ParseCsvOutput_WindowsLineEndings_ParsedCorrectly()
    {
        var csv = @"\\.\DISPLAY1,GSM59A4,GSM59A4-1234,Active,\\.\DISPLAY1\GSM59A4,LG ULTRAGEAR,ABC123,NVIDIA,3840x2160,0,3840,2160,32,144,Yes" + "\r\n"
                + @"\\.\DISPLAY2,DEL4321,DEL4321-5678,Active,\\.\DISPLAY2\DEL4321,Dell U2723QE,XYZ789,NVIDIA,2560x1440,0,2560,1440,32,60,No" + "\r\n";
        var monitors = MonitorService.ParseCsvOutput(csv);

        monitors.Count.ShouldBe(2);
    }

    [Fact]
    public void ParseCsvOutput_QuotedFieldsWithCommas_ParsedCorrectly()
    {
        var csv = @"\\.\DISPLAY1,GSM59A4,GSM59A4-1234,Active,""\\.\DISPLAY1, special"",""LG ULTRA, GEAR"",ABC123,NVIDIA,3840x2160,0,3840,2160,32,144,Yes";
        var monitors = MonitorService.ParseCsvOutput(csv);

        monitors.Count.ShouldBe(1);
        monitors[0].MonitorName.ShouldBe("LG ULTRA, GEAR");
    }

    // ── FindMonitor / ID matching tests ───────────────────────────────

    [Fact]
    public void FindMonitor_MatchByDisplayName_ReturnsMonitor()
    {
        var monitors = MonitorService.ParseCsvOutput(SampleCsv);

        var result = MonitorService.FindMonitor(monitors, @"\\.\DISPLAY2");

        result.MonitorId.ShouldBe("DEL4321");
    }

    [Fact]
    public void FindMonitor_MatchByShortMonitorId_ReturnsMonitor()
    {
        var monitors = MonitorService.ParseCsvOutput(SampleCsv);

        var result = MonitorService.FindMonitor(monitors, "DEL4321");

        result.Name.ShouldBe(@"\\.\DISPLAY2");
    }

    [Fact]
    public void FindMonitor_MatchBySerialNumber_ReturnsMonitor()
    {
        var monitors = MonitorService.ParseCsvOutput(SampleCsv);

        var result = MonitorService.FindMonitor(monitors, "XYZ789");

        result.Name.ShouldBe(@"\\.\DISPLAY2");
    }

    [Fact]
    public void FindMonitor_CaseInsensitive_ReturnsMonitor()
    {
        var monitors = MonitorService.ParseCsvOutput(SampleCsv);

        var result = MonitorService.FindMonitor(monitors, "gsm59a4");

        result.Name.ShouldBe(@"\\.\DISPLAY1");
    }

    [Fact]
    public void FindMonitor_UnknownId_ThrowsKeyNotFoundException()
    {
        var monitors = MonitorService.ParseCsvOutput(SampleCsv);

        Should.Throw<KeyNotFoundException>(
            () => MonitorService.FindMonitor(monitors, "UNKNOWN123"));
    }

    [Fact]
    public void FindMonitor_EmptyList_ThrowsKeyNotFoundException()
    {
        var monitors = new List<MonitorInfo>();

        Should.Throw<KeyNotFoundException>(
            () => MonitorService.FindMonitor(monitors, "GSM59A4"));
    }

    [Fact]
    public void FindMonitor_NullSerialNotMatchedByEmptyString()
    {
        // Samsung monitor has no serial — searching empty string should not match it
        var monitors = MonitorService.ParseCsvOutput(SampleCsv);

        Should.Throw<KeyNotFoundException>(
            () => MonitorService.FindMonitor(monitors, ""));
    }

    // ── Async method tests (mocked ICliRunner) ────────────────────────

    [Fact]
    public async Task GetMonitorsAsync_CallsCliRunnerAndParsesOutput()
    {
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        var service = CreateService();

        var monitors = await service.GetMonitorsAsync();

        monitors.Count.ShouldBe(3);
        A.CallTo(() => _cliRunner.RunAsync(
            A<string>.That.EndsWith("MultiMonitorTool.exe"),
            A<IEnumerable<string>>.That.IsSameSequenceAs(new[] { "/scomma", "" }),
            A<int>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task EnableMonitorAsync_ResolvesAndCallsCliRunner()
    {
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        var service = CreateService();

        await service.EnableMonitorAsync("DEL4321");

        A.CallTo(() => _cliRunner.RunAsync(
            A<string>._,
            A<IEnumerable<string>>.That.IsSameSequenceAs(new[] { "/enable", @"\\.\DISPLAY2" }),
            A<int>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DisableMonitorAsync_ResolvesAndCallsCliRunner()
    {
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        var service = CreateService();

        await service.DisableMonitorAsync("GSM59A4");

        A.CallTo(() => _cliRunner.RunAsync(
            A<string>._,
            A<IEnumerable<string>>.That.IsSameSequenceAs(new[] { "/disable", @"\\.\DISPLAY1" }),
            A<int>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SetPrimaryAsync_ResolvesAndCallsCliRunner()
    {
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        var service = CreateService();

        await service.SetPrimaryAsync("XYZ789");

        A.CallTo(() => _cliRunner.RunAsync(
            A<string>._,
            A<IEnumerable<string>>.That.IsSameSequenceAs(new[] { "/SetPrimary", @"\\.\DISPLAY2" }),
            A<int>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task EnableMonitorAsync_UnknownId_ThrowsKeyNotFoundException()
    {
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        var service = CreateService();

        await Should.ThrowAsync<KeyNotFoundException>(
            () => service.EnableMonitorAsync("UNKNOWN"));
    }

    [Fact]
    public async Task SoloMonitorAsync_DisablesOthersAndEnablesTarget()
    {
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        var service = CreateService();

        await service.SoloMonitorAsync("DEL4321");

        // Should disable DISPLAY1 and DISPLAY3 (both active, not the target)
        A.CallTo(() => _cliRunner.RunAsync(
            A<string>._,
            A<IEnumerable<string>>.That.IsSameSequenceAs(new[] { "/disable", @"\\.\DISPLAY1" }),
            A<int>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _cliRunner.RunAsync(
            A<string>._,
            A<IEnumerable<string>>.That.IsSameSequenceAs(new[] { "/disable", @"\\.\DISPLAY3" }),
            A<int>._)).MustHaveHappenedOnceExactly();
        // Should set target as primary
        A.CallTo(() => _cliRunner.RunAsync(
            A<string>._,
            A<IEnumerable<string>>.That.IsSameSequenceAs(new[] { "/SetPrimary", @"\\.\DISPLAY2" }),
            A<int>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SoloMonitorAsync_UnknownId_ThrowsKeyNotFoundException()
    {
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        var service = CreateService();

        await Should.ThrowAsync<KeyNotFoundException>(
            () => service.SoloMonitorAsync("UNKNOWN"));
    }
}
