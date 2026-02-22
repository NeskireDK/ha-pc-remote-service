using FakeItEasy;
using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Services;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HaPcRemote.Service.Tests.Services;

public class AudioServiceTests
{
    // SoundVolumeView /scomma with /Columns "Name,Direction,Default,Volume Percent"
    // Columns: [0] Name, [1] Direction, [2] Default (Console), [3] Volume Percent
    // Default column = "Render" for default render device, empty for non-default
    // This format matches SoundVolumeView v2.47+ on Windows 10/11
    private const string SampleCsv =
        """
        Speakers,Render,Render,50.0%
        Headphones,Render,,75.5%
        Microphone,Capture,Capture,80.0%
        """;

    private readonly ICliRunner _cliRunner = A.Fake<ICliRunner>();

    private AudioService CreateService(string toolsPath = "./tools")
    {
        var monitor = A.Fake<IOptionsMonitor<PcRemoteOptions>>();
        A.CallTo(() => monitor.CurrentValue).Returns(new PcRemoteOptions
        {
            ToolsPath = toolsPath
        });
        return new AudioService(monitor, _cliRunner);
    }

    // ── CSV parsing tests (static) ────────────────────────────────────

    [Fact]
    public void ParseCsvOutput_FiltersToRenderDevicesOnly()
    {
        var devices = AudioService.ParseCsvOutput(SampleCsv);

        devices.Count.ShouldBe(2);
        devices.ShouldAllBe(d => d.Name != "Microphone");
    }

    [Fact]
    public void ParseCsvOutput_ParsesDeviceName()
    {
        var devices = AudioService.ParseCsvOutput(SampleCsv);

        devices[0].Name.ShouldBe("Speakers");
        devices[1].Name.ShouldBe("Headphones");
    }

    [Fact]
    public void ParseCsvOutput_ParsesVolumePercent()
    {
        var devices = AudioService.ParseCsvOutput(SampleCsv);

        devices[0].Volume.ShouldBe(50);
        devices[1].Volume.ShouldBe(76); // 75.5 rounds to 76
    }

    [Fact]
    public void ParseCsvOutput_IdentifiesDefaultDevice()
    {
        var devices = AudioService.ParseCsvOutput(SampleCsv);

        devices[0].IsDefault.ShouldBeTrue();
        devices[1].IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void ParseCsvOutput_EmptyInput_ReturnsEmptyList()
    {
        var devices = AudioService.ParseCsvOutput("");

        devices.ShouldBeEmpty();
    }

    [Fact]
    public void ParseCsvOutput_WhitespaceOnlyInput_ReturnsEmptyList()
    {
        var devices = AudioService.ParseCsvOutput("   \n  \n  ");

        devices.ShouldBeEmpty();
    }

    [Fact]
    public void ParseCsvOutput_LineTooShort_IsSkipped()
    {
        var csv = "Speakers,Render,Render";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.ShouldBeEmpty();
    }

    [Fact]
    public void ParseCsvOutput_CaptureDevicesExcluded()
    {
        var csv = "Microphone,Capture,Capture,100.0%";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.ShouldBeEmpty();
    }

    [Fact]
    public void ParseCsvOutput_NoDefaultDevice_AllIsDefaultFalse()
    {
        var csv = "Speakers,Render,,50.0%";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.Count.ShouldBe(1);
        devices[0].IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void ParseCsvOutput_VolumeWithoutPercent_ParsesAsZero()
    {
        var csv = "Speakers,Render,Render,invalid";
        var devices = AudioService.ParseCsvOutput(csv);

        devices[0].Volume.ShouldBe(0);
    }

    [Fact]
    public void ParseCsvOutput_QuotedFieldsWithCommas_ParsedCorrectly()
    {
        var csv = "\"Speakers, Front\",Render,Render,50.0%";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.Count.ShouldBe(1);
        devices[0].Name.ShouldBe("Speakers, Front");
    }

    [Fact]
    public void ParseCsvOutput_WindowsLineEndings_ParsedCorrectly()
    {
        var csv = "Speakers,Render,Render,50.0%\r\nHeadphones,Render,,75.0%\r\n";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.Count.ShouldBe(2);
    }

    [Fact]
    public void ParseCsvOutput_DuplicateDeviceNames_DeduplicatedToFirst()
    {
        var csv = """
            Speakers,Render,Render,50.0%
            Speakers,Render,,30.0%
            Speakers,Render,,20.0%
            Headphones,Render,,75.0%
            """;
        var devices = AudioService.ParseCsvOutput(csv);

        devices.Count.ShouldBe(2);
        devices[0].Name.ShouldBe("Speakers");
        devices[0].Volume.ShouldBe(50); // keeps first entry
        devices[1].Name.ShouldBe("Headphones");
    }

    // ── Async method tests (mocked ICliRunner) ────────────────────────

    [Fact]
    public async Task GetDevicesAsync_CallsCliRunnerWithCorrectArgs()
    {
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        var service = CreateService();

        var devices = await service.GetDevicesAsync();

        devices.Count.ShouldBe(2);
        A.CallTo(() => _cliRunner.RunAsync(
            A<string>.That.EndsWith("SoundVolumeView.exe"),
            A<IEnumerable<string>>.That.Contains("/scomma"),
            A<int>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetCurrentDeviceAsync_ReturnsDefaultDevice()
    {
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        var service = CreateService();

        var device = await service.GetCurrentDeviceAsync();

        device.ShouldNotBeNull();
        device.Name.ShouldBe("Speakers");
        device.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task GetCurrentDeviceAsync_NoDefault_ReturnsNull()
    {
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns("Speakers,Render,,50.0%");
        var service = CreateService();

        var device = await service.GetCurrentDeviceAsync();

        device.ShouldBeNull();
    }

    [Fact]
    public async Task SetDefaultDeviceAsync_CallsCliRunnerWithDeviceName()
    {
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        var service = CreateService();

        await service.SetDefaultDeviceAsync("Headphones");

        A.CallTo(() => _cliRunner.RunAsync(
            A<string>.That.EndsWith("SoundVolumeView.exe"),
            A<IEnumerable<string>>.That.IsSameSequenceAs(new[] { "/SetDefault", "Headphones", "1" }),
            A<int>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SetDefaultDeviceAsync_InvalidDevice_ThrowsKeyNotFoundException()
    {
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        var service = CreateService();

        await Should.ThrowAsync<KeyNotFoundException>(
            () => service.SetDefaultDeviceAsync("NonExistentDevice"));
    }

    [Fact]
    public async Task SetVolumeAsync_SetsVolumeAndUnmutes()
    {
        // First call returns devices (for GetCurrentDeviceAsync), subsequent calls are set/unmute
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        var service = CreateService();

        await service.SetVolumeAsync(75);

        // Should call: GetDevicesAsync, SetVolume, Unmute = 3 calls total
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .MustHaveHappened(3, Times.Exactly);
        A.CallTo(() => _cliRunner.RunAsync(
            A<string>._,
            A<IEnumerable<string>>.That.IsSameSequenceAs(new[] { "/SetVolume", "Speakers", "75" }),
            A<int>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _cliRunner.RunAsync(
            A<string>._,
            A<IEnumerable<string>>.That.IsSameSequenceAs(new[] { "/Unmute", "Speakers" }),
            A<int>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SetVolumeAsync_NoDefaultDevice_ThrowsInvalidOperationException()
    {
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns("Speakers,Render,,50.0%"); // No default device
        var service = CreateService();

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.SetVolumeAsync(50));
    }

    [Fact]
    public async Task GetDevicesAsync_UsesToolsPathFromOptions()
    {
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns("");
        var service = CreateService("C:\\custom\\tools");

        await service.GetDevicesAsync();

        A.CallTo(() => _cliRunner.RunAsync(
            A<string>.That.StartsWith("C:\\custom\\tools"),
            A<IEnumerable<string>>._,
            A<int>._)).MustHaveHappenedOnceExactly();
    }
}
