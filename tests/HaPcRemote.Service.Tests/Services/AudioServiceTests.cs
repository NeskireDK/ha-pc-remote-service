using FakeItEasy;
using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Services;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HaPcRemote.Service.Tests.Services;

public class AudioServiceTests
{
    // SoundVolumeView /scomma with /Columns "Type,Name,Direction,Default,Volume Percent"
    // Columns: [0] Type, [1] Name, [2] Direction, [3] Default (Console), [4] Volume Percent
    // Type = "Device" for hardware sound card devices; "Application"/"Subunit" for virtual/software entries
    // Default column = "Render" for default render device, empty for non-default
    // This format matches SoundVolumeView v2.47+ on Windows 10/11
    private const string SampleCsv =
        """
        Device,Speakers,Render,Render,50.0%
        Device,Headphones,Render,,75.5%
        Device,Microphone,Capture,Capture,80.0%
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
        var csv = "Device,Speakers,Render,Render";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.ShouldBeEmpty();
    }

    [Fact]
    public void ParseCsvOutput_CaptureDevicesExcluded()
    {
        var csv = "Device,Microphone,Capture,Capture,100.0%";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.ShouldBeEmpty();
    }

    [Fact]
    public void ParseCsvOutput_NoDefaultDevice_AllIsDefaultFalse()
    {
        var csv = "Device,Speakers,Render,,50.0%";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.Count.ShouldBe(1);
        devices[0].IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void ParseCsvOutput_VolumeWithoutPercent_ParsesAsZero()
    {
        var csv = "Device,Speakers,Render,Render,invalid";
        var devices = AudioService.ParseCsvOutput(csv);

        devices[0].Volume.ShouldBe(0);
    }

    [Fact]
    public void ParseCsvOutput_QuotedFieldsWithCommas_ParsedCorrectly()
    {
        var csv = "Device,\"Speakers, Front\",Render,Render,50.0%";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.Count.ShouldBe(1);
        devices[0].Name.ShouldBe("Speakers, Front");
    }

    [Fact]
    public void ParseCsvOutput_WindowsLineEndings_ParsedCorrectly()
    {
        var csv = "Device,Speakers,Render,Render,50.0%\r\nDevice,Headphones,Render,,75.0%\r\n";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.Count.ShouldBe(2);
    }

    [Fact]
    public void ParseCsvOutput_DuplicateDeviceNames_DeduplicatedToFirst()
    {
        var csv = """
            Device,Speakers,Render,Render,50.0%
            Device,Speakers,Render,,30.0%
            Device,Speakers,Render,,20.0%
            Device,Headphones,Render,,75.0%
            """;
        var devices = AudioService.ParseCsvOutput(csv);

        devices.Count.ShouldBe(2);
        devices[0].Name.ShouldBe("Speakers");
        devices[0].Volume.ShouldBe(50); // keeps first entry
        devices[1].Name.ShouldBe("Headphones");
    }

    [Fact]
    public void ParseCsvOutput_VirtualAudioDevicesExcluded()
    {
        var csv = """
            Device,Speakers,Render,Render,50.0%
            Application,Discord,Render,,30.0%
            Subunit,Steam Streaming Speakers,Render,,0.0%
            Device,Headphones,Render,,75.0%
            """;
        var devices = AudioService.ParseCsvOutput(csv);

        devices.Count.ShouldBe(2);
        devices[0].Name.ShouldBe("Speakers");
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
            .Returns("Device,Speakers,Render,,50.0%");
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
            .Returns("Device,Speakers,Render,,50.0%"); // No default device
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

    // ── CSV parsing boundary cases ────────────────────────────────────

    [Fact]
    public void ParseCsvOutput_NullLikeOnlyCommas_IsSkipped()
    {
        // Line has 5 columns but type is not "Device" — should be excluded
        var csv = "Application,VoiceMeeter,Render,Render,50.0%";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.ShouldBeEmpty();
    }

    [Fact]
    public void ParseCsvOutput_DeviceNameWithSpaces_ParsedCorrectly()
    {
        var csv = "Device,Realtek High Definition Audio,Render,Render,60.0%";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.Count.ShouldBe(1);
        devices[0].Name.ShouldBe("Realtek High Definition Audio");
    }

    [Fact]
    public void ParseCsvOutput_VolumeZeroPercent_ParsesAsZero()
    {
        var csv = "Device,Speakers,Render,Render,0.0%";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.Count.ShouldBe(1);
        devices[0].Volume.ShouldBe(0);
    }

    [Fact]
    public void ParseCsvOutput_VolumeOneHundredPercent_ParsesAs100()
    {
        var csv = "Device,Speakers,Render,Render,100.0%";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.Count.ShouldBe(1);
        devices[0].Volume.ShouldBe(100);
    }

    [Fact]
    public void ParseCsvOutput_DeviceNameWithSpecialChars_ParsedCorrectly()
    {
        var csv = "Device,USB Audio (2.0) [HID],Render,Render,45.0%";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.Count.ShouldBe(1);
        devices[0].Name.ShouldBe("USB Audio (2.0) [HID]");
    }

    // ── Async boundary cases ──────────────────────────────────────────

    [Fact]
    public async Task GetDevicesAsync_EmptyOutput_ReturnsEmptyList()
    {
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(string.Empty);
        var service = CreateService();

        var devices = await service.GetDevicesAsync();

        devices.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetDefaultDeviceAsync_DeviceNameWithSpaces_CallsCliRunnerCorrectly()
    {
        var csv = "Device,Realtek High Definition Audio,Render,Render,60.0%";
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(csv);
        var service = CreateService();

        await service.SetDefaultDeviceAsync("Realtek High Definition Audio");

        A.CallTo(() => _cliRunner.RunAsync(
            A<string>.That.EndsWith("SoundVolumeView.exe"),
            A<IEnumerable<string>>.That.IsSameSequenceAs(
                new[] { "/SetDefault", "Realtek High Definition Audio", "1" }),
            A<int>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SetVolumeAsync_ZeroVolume_CallsCliRunnerWithZero()
    {
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        var service = CreateService();

        await service.SetVolumeAsync(0);

        A.CallTo(() => _cliRunner.RunAsync(
            A<string>._,
            A<IEnumerable<string>>.That.IsSameSequenceAs(new[] { "/SetVolume", "Speakers", "0" }),
            A<int>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SetVolumeAsync_100Volume_CallsCliRunnerWith100()
    {
        A.CallTo(() => _cliRunner.RunAsync(A<string>._, A<IEnumerable<string>>._, A<int>._))
            .Returns(SampleCsv);
        var service = CreateService();

        await service.SetVolumeAsync(100);

        A.CallTo(() => _cliRunner.RunAsync(
            A<string>._,
            A<IEnumerable<string>>.That.IsSameSequenceAs(new[] { "/SetVolume", "Speakers", "100" }),
            A<int>._)).MustHaveHappenedOnceExactly();
    }
}
