using FakeItEasy;
using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Models;
using HaPcRemote.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HaPcRemote.Service.Tests.Services;

public class MonitorServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ICliRunner _cliRunner = A.Fake<ICliRunner>();

    private void SetupCliRunnerWithXml(string xml = SampleXml)
    {
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Invokes((string _, IEnumerable<string> args, int _) =>
            {
                var argList = args.ToList();
                if (argList.Count >= 2 && argList[0] == "/sxml" && !string.IsNullOrEmpty(argList[1]))
                    File.WriteAllText(argList[1], xml);
            })
            .Returns(string.Empty);
    }

    // Realistic MultiMonitorTool /sxml output based on actual tool output
    private const string SampleXml =
        """
        <?xml version="1.0" ?>
        <monitors_list>
        <item>
        <resolution>3840 X 2160</resolution>
        <left-top>0, 0</left-top>
        <right-bottom>3840, 2160</right-bottom>
        <active>Yes</active>
        <disconnected>No</disconnected>
        <primary>Yes</primary>
        <colors>32</colors>
        <frequency>144</frequency>
        <orientation>Default</orientation>
        <maximum_resolution>3840 X 2160</maximum_resolution>
        <current_scale>100%</current_scale>
        <maximum_scale>200%</maximum_scale>
        <name>\\.\DISPLAY1</name>
        <adapter>NVIDIA GeForce</adapter>
        <device_id>PCI\VEN_10DE</device_id>
        <device_key>\Registry\Machine\System\CurrentControlSet\Control\Video\{ABC}\0000</device_key>
        <monitor_id>MONITOR\GSM59A4\{4d36e96e-e325-11ce-bfc1-08002be10318}\0001</monitor_id>
        <short_monitor_id>GSM59A4</short_monitor_id>
        <monitor_key>\Registry\Machine\System\CurrentControlSet\Control\Class\{4d36e96e}\0001</monitor_key>
        <monitor_string>LG ULTRAGEAR</monitor_string>
        <monitor_name>LG ULTRAGEAR</monitor_name>
        <monitor_serial_number>ABC123</monitor_serial_number>
        </item>
        <item>
        <resolution>2560 X 1440</resolution>
        <left-top>3840, 0</left-top>
        <right-bottom>6400, 1440</right-bottom>
        <active>Yes</active>
        <disconnected>No</disconnected>
        <primary>No</primary>
        <colors>32</colors>
        <frequency>60</frequency>
        <orientation>Default</orientation>
        <maximum_resolution>2560 X 1440</maximum_resolution>
        <current_scale>100%</current_scale>
        <maximum_scale>200%</maximum_scale>
        <name>\\.\DISPLAY2</name>
        <adapter>NVIDIA GeForce</adapter>
        <device_id>PCI\VEN_10DE</device_id>
        <device_key>\Registry\Machine\System\CurrentControlSet\Control\Video\{DEF}\0001</device_key>
        <monitor_id>MONITOR\DEL4321\{4d36e96e-e325-11ce-bfc1-08002be10318}\0002</monitor_id>
        <short_monitor_id>DEL4321</short_monitor_id>
        <monitor_key>\Registry\Machine\System\CurrentControlSet\Control\Class\{4d36e96e}\0002</monitor_key>
        <monitor_string>Dell U2723QE</monitor_string>
        <monitor_name>Dell U2723QE</monitor_name>
        <monitor_serial_number>XYZ789</monitor_serial_number>
        </item>
        <item>
        <resolution>1920 X 1080</resolution>
        <left-top>6400, 0</left-top>
        <right-bottom>8320, 1080</right-bottom>
        <active>Yes</active>
        <disconnected>No</disconnected>
        <primary>No</primary>
        <colors>32</colors>
        <frequency>240</frequency>
        <orientation>Default</orientation>
        <maximum_resolution>1920 X 1080</maximum_resolution>
        <current_scale>100%</current_scale>
        <maximum_scale>200%</maximum_scale>
        <name>\\.\DISPLAY3</name>
        <adapter>NVIDIA GeForce</adapter>
        <device_id>PCI\VEN_10DE</device_id>
        <device_key>\Registry\Machine\System\CurrentControlSet\Control\Video\{GHI}\0002</device_key>
        <monitor_id>MONITOR\SAM0F00\{4d36e96e-e325-11ce-bfc1-08002be10318}\0003</monitor_id>
        <short_monitor_id>SAM0F00</short_monitor_id>
        <monitor_key>\Registry\Machine\System\CurrentControlSet\Control\Class\{4d36e96e}\0003</monitor_key>
        <monitor_string>Samsung Odyssey</monitor_string>
        <monitor_name>Samsung Odyssey</monitor_name>
        <monitor_serial_number></monitor_serial_number>
        </item>
        </monitors_list>
        """;

    private const string XmlWithDisconnected =
        """
        <?xml version="1.0" ?>
        <monitors_list>
        <item>
        <resolution>3840 X 2160</resolution>
        <active>Yes</active>
        <disconnected>No</disconnected>
        <primary>Yes</primary>
        <frequency>144</frequency>
        <name>\\.\DISPLAY1</name>
        <short_monitor_id>GSM59A4</short_monitor_id>
        <monitor_name>LG ULTRAGEAR</monitor_name>
        <monitor_serial_number>ABC123</monitor_serial_number>
        </item>
        <item>
        <resolution></resolution>
        <active>No</active>
        <disconnected>Yes</disconnected>
        <primary>No</primary>
        <frequency>0</frequency>
        <name>\\.\DISPLAY4</name>
        <short_monitor_id>AOC1234</short_monitor_id>
        <monitor_name>AOC Monitor</monitor_name>
        <monitor_serial_number>DISC01</monitor_serial_number>
        </item>
        </monitors_list>
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
        new MonitorService(CreateOptions(profilesPath), _cliRunner, A.Fake<ILogger<MonitorService>>());

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

    // ── XML parsing tests ─────────────────────────────────────────────

    [Fact]
    public void ParseXmlOutput_ParsesAllConnectedMonitors()
    {
        var monitors = MonitorService.ParseXmlOutput(SampleXml);

        monitors.Count.ShouldBe(3);
    }

    [Fact]
    public void ParseXmlOutput_ParsesDisplayName()
    {
        var monitors = MonitorService.ParseXmlOutput(SampleXml);

        monitors[0].Name.ShouldBe(@"\\.\DISPLAY1");
        monitors[1].Name.ShouldBe(@"\\.\DISPLAY2");
    }

    [Fact]
    public void ParseXmlOutput_ParsesShortMonitorId()
    {
        var monitors = MonitorService.ParseXmlOutput(SampleXml);

        monitors[0].MonitorId.ShouldBe("GSM59A4");
        monitors[1].MonitorId.ShouldBe("DEL4321");
    }

    [Fact]
    public void ParseXmlOutput_ParsesFriendlyName()
    {
        var monitors = MonitorService.ParseXmlOutput(SampleXml);

        monitors[0].MonitorName.ShouldBe("LG ULTRAGEAR");
        monitors[1].MonitorName.ShouldBe("Dell U2723QE");
    }

    [Fact]
    public void ParseXmlOutput_ParsesSerialNumber()
    {
        var monitors = MonitorService.ParseXmlOutput(SampleXml);

        monitors[0].SerialNumber.ShouldBe("ABC123");
        monitors[1].SerialNumber.ShouldBe("XYZ789");
    }

    [Fact]
    public void ParseXmlOutput_EmptySerialNumber_ReturnsNull()
    {
        var monitors = MonitorService.ParseXmlOutput(SampleXml);

        monitors[2].SerialNumber.ShouldBeNull();
    }

    [Fact]
    public void ParseXmlOutput_ParsesResolution()
    {
        var monitors = MonitorService.ParseXmlOutput(SampleXml);

        monitors[0].Width.ShouldBe(3840);
        monitors[0].Height.ShouldBe(2160);
        monitors[1].Width.ShouldBe(2560);
        monitors[1].Height.ShouldBe(1440);
    }

    [Fact]
    public void ParseXmlOutput_ParsesDisplayFrequency()
    {
        var monitors = MonitorService.ParseXmlOutput(SampleXml);

        monitors[0].DisplayFrequency.ShouldBe(144);
        monitors[1].DisplayFrequency.ShouldBe(60);
        monitors[2].DisplayFrequency.ShouldBe(240);
    }

    [Fact]
    public void ParseXmlOutput_IdentifiesPrimaryMonitor()
    {
        var monitors = MonitorService.ParseXmlOutput(SampleXml);

        monitors[0].IsPrimary.ShouldBeTrue();
        monitors[1].IsPrimary.ShouldBeFalse();
        monitors[2].IsPrimary.ShouldBeFalse();
    }

    [Fact]
    public void ParseXmlOutput_ActiveMonitors_AreActive()
    {
        var monitors = MonitorService.ParseXmlOutput(SampleXml);

        monitors.ShouldAllBe(m => m.IsActive);
    }

    [Fact]
    public void ParseXmlOutput_FiltersDisconnectedMonitors()
    {
        var monitors = MonitorService.ParseXmlOutput(XmlWithDisconnected);

        monitors.Count.ShouldBe(1);
        monitors[0].Name.ShouldBe(@"\\.\DISPLAY1");
    }

    [Fact]
    public void ParseXmlOutput_EmptyInput_ReturnsEmptyList()
    {
        var monitors = MonitorService.ParseXmlOutput("");

        monitors.ShouldBeEmpty();
    }

    [Fact]
    public void ParseXmlOutput_WhitespaceOnlyInput_ReturnsEmptyList()
    {
        var monitors = MonitorService.ParseXmlOutput("   \n  \n  ");

        monitors.ShouldBeEmpty();
    }

    [Fact]
    public void ParseXmlOutput_ItemWithEmptyName_IsSkipped()
    {
        var xml = """
            <?xml version="1.0" ?>
            <monitors_list>
              <item>
                <name></name>
                <short_monitor_id>X</short_monitor_id>
                <resolution>1920 X 1080</resolution>
                <active>Yes</active>
                <disconnected>No</disconnected>
              </item>
            </monitors_list>
            """;
        var monitors = MonitorService.ParseXmlOutput(xml);

        monitors.ShouldBeEmpty();
    }

    [Fact]
    public void ParseXmlOutput_MissingOptionalElements_UsesDefaults()
    {
        var xml = """
            <?xml version="1.0" ?>
            <monitors_list>
              <item>
                <name>\\.\DISPLAY1</name>
              </item>
            </monitors_list>
            """;
        var monitors = MonitorService.ParseXmlOutput(xml);

        monitors.Count.ShouldBe(1);
        monitors[0].MonitorId.ShouldBe("");
        monitors[0].MonitorName.ShouldBe("");
        monitors[0].SerialNumber.ShouldBeNull();
        monitors[0].Width.ShouldBe(0);
        monitors[0].Height.ShouldBe(0);
        monitors[0].DisplayFrequency.ShouldBe(0);
        monitors[0].IsActive.ShouldBeFalse();
        monitors[0].IsPrimary.ShouldBeFalse();
    }

    // ── Resolution parsing tests ──────────────────────────────────────

    [Fact]
    public void ParseResolution_StandardFormat_ParsesCorrectly()
    {
        MonitorService.ParseResolution("1920 X 1200", out var w, out var h);

        w.ShouldBe(1920);
        h.ShouldBe(1200);
    }

    [Fact]
    public void ParseResolution_4K_ParsesCorrectly()
    {
        MonitorService.ParseResolution("3840 X 2160", out var w, out var h);

        w.ShouldBe(3840);
        h.ShouldBe(2160);
    }

    [Fact]
    public void ParseResolution_Null_ReturnsZeros()
    {
        MonitorService.ParseResolution(null, out var w, out var h);

        w.ShouldBe(0);
        h.ShouldBe(0);
    }

    [Fact]
    public void ParseResolution_Empty_ReturnsZeros()
    {
        MonitorService.ParseResolution("", out var w, out var h);

        w.ShouldBe(0);
        h.ShouldBe(0);
    }

    [Fact]
    public void ParseResolution_MalformedInput_ReturnsZeros()
    {
        MonitorService.ParseResolution("not a resolution", out var w, out var h);

        w.ShouldBe(0);
        h.ShouldBe(0);
    }

    // ── FindMonitor / ID matching tests ───────────────────────────────

    [Fact]
    public void FindMonitor_MatchByDisplayName_ReturnsMonitor()
    {
        var monitors = MonitorService.ParseXmlOutput(SampleXml);

        var result = MonitorService.FindMonitor(monitors, @"\\.\DISPLAY2");

        result.MonitorId.ShouldBe("DEL4321");
    }

    [Fact]
    public void FindMonitor_MatchByShortMonitorId_ReturnsMonitor()
    {
        var monitors = MonitorService.ParseXmlOutput(SampleXml);

        var result = MonitorService.FindMonitor(monitors, "DEL4321");

        result.Name.ShouldBe(@"\\.\DISPLAY2");
    }

    [Fact]
    public void FindMonitor_MatchBySerialNumber_ReturnsMonitor()
    {
        var monitors = MonitorService.ParseXmlOutput(SampleXml);

        var result = MonitorService.FindMonitor(monitors, "XYZ789");

        result.Name.ShouldBe(@"\\.\DISPLAY2");
    }

    [Fact]
    public void FindMonitor_CaseInsensitive_ReturnsMonitor()
    {
        var monitors = MonitorService.ParseXmlOutput(SampleXml);

        var result = MonitorService.FindMonitor(monitors, "gsm59a4");

        result.Name.ShouldBe(@"\\.\DISPLAY1");
    }

    [Fact]
    public void FindMonitor_UnknownId_ThrowsKeyNotFoundException()
    {
        var monitors = MonitorService.ParseXmlOutput(SampleXml);

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
        var monitors = MonitorService.ParseXmlOutput(SampleXml);

        Should.Throw<KeyNotFoundException>(
            () => MonitorService.FindMonitor(monitors, ""));
    }

    // ── Async method tests (mocked ICliRunner) ────────────────────────

    [Fact]
    public async Task GetMonitorsAsync_CallsCliRunnerAndParsesOutput()
    {
        SetupCliRunnerWithXml();
        var service = CreateService();

        var monitors = await service.GetMonitorsAsync();

        monitors.Count.ShouldBe(3);
        A.CallTo(() => _cliRunner.RunAsync(
            A<string>.That.EndsWith("MultiMonitorTool.exe"),
            A<IEnumerable<string>>.That.Matches(a =>
                a.First() == "/sxml" && a.Last() != ""),
            A<int>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task EnableMonitorAsync_ResolvesAndCallsCliRunner()
    {
        SetupCliRunnerWithXml();
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
        SetupCliRunnerWithXml();
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
        SetupCliRunnerWithXml();
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
        SetupCliRunnerWithXml();
        var service = CreateService();

        await Should.ThrowAsync<KeyNotFoundException>(
            () => service.EnableMonitorAsync("UNKNOWN"));
    }

    [Fact]
    public async Task SoloMonitorAsync_TargetAlreadyActive_SetsPrimaryThenDisablesOthers()
    {
        // DEL4321 (DISPLAY2) is already active in SampleXml
        var calls = new List<string[]>();
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Invokes((string _, IEnumerable<string> args, int _) =>
            {
                var argList = args.ToList();
                if (argList.Count >= 2 && argList[0] == "/sxml" && !string.IsNullOrEmpty(argList[1]))
                    File.WriteAllText(argList[1], SampleXml);
                else
                    calls.Add(argList.ToArray());
            })
            .Returns(string.Empty);

        var service = CreateService();

        await service.SoloMonitorAsync("DEL4321");

        // Must NOT enable the target (it is already active)
        calls.ShouldNotContain(c => c[0] == "/enable" && c[1] == @"\\.\DISPLAY2");

        // First mutating call must be SetPrimary on the target
        calls[0][0].ShouldBe("/SetPrimary");
        calls[0][1].ShouldBe(@"\\.\DISPLAY2");

        // Then disables of the other active monitors (DISPLAY1 and DISPLAY3)
        calls.ShouldContain(c => c[0] == "/disable" && c[1] == @"\\.\DISPLAY1");
        calls.ShouldContain(c => c[0] == "/disable" && c[1] == @"\\.\DISPLAY3");

        // No disable on the target
        calls.ShouldNotContain(c => c[0] == "/disable" && c[1] == @"\\.\DISPLAY2");
    }

    [Fact]
    public async Task SoloMonitorAsync_TargetInactive_EnablesThenSetsPrimaryThenDisablesOthers()
    {
        // Use XML where DISPLAY2 is inactive so the enable step fires
        const string xmlWithInactiveTarget =
            """
            <?xml version="1.0" ?>
            <monitors_list>
            <item>
            <resolution>3840 X 2160</resolution>
            <active>Yes</active>
            <disconnected>No</disconnected>
            <primary>Yes</primary>
            <frequency>144</frequency>
            <name>\\.\DISPLAY1</name>
            <short_monitor_id>GSM59A4</short_monitor_id>
            <monitor_name>LG ULTRAGEAR</monitor_name>
            <monitor_serial_number>ABC123</monitor_serial_number>
            </item>
            <item>
            <resolution>2560 X 1440</resolution>
            <active>No</active>
            <disconnected>No</disconnected>
            <primary>No</primary>
            <frequency>60</frequency>
            <name>\\.\DISPLAY2</name>
            <short_monitor_id>DEL4321</short_monitor_id>
            <monitor_name>Dell U2723QE</monitor_name>
            <monitor_serial_number>XYZ789</monitor_serial_number>
            </item>
            </monitors_list>
            """;

        var calls = new List<string[]>();
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Invokes((string _, IEnumerable<string> args, int _) =>
            {
                var argList = args.ToList();
                if (argList.Count >= 2 && argList[0] == "/sxml" && !string.IsNullOrEmpty(argList[1]))
                    File.WriteAllText(argList[1], xmlWithInactiveTarget);
                else
                    calls.Add(argList.ToArray());
            })
            .Returns(string.Empty);

        var service = CreateService();

        await service.SoloMonitorAsync("DEL4321");

        // Order: enable target, set primary, disable others
        calls[0][0].ShouldBe("/enable");
        calls[0][1].ShouldBe(@"\\.\DISPLAY2");

        calls[1][0].ShouldBe("/SetPrimary");
        calls[1][1].ShouldBe(@"\\.\DISPLAY2");

        calls[2][0].ShouldBe("/disable");
        calls[2][1].ShouldBe(@"\\.\DISPLAY1");

        calls.Count.ShouldBe(3);
    }

    [Fact]
    public async Task SoloMonitorAsync_UnknownId_ThrowsKeyNotFoundException()
    {
        SetupCliRunnerWithXml();
        var service = CreateService();

        await Should.ThrowAsync<KeyNotFoundException>(
            () => service.SoloMonitorAsync("UNKNOWN"));
    }
}
