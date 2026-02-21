using HaPcRemote.Service.Services;
using Shouldly;

namespace HaPcRemote.Service.Tests.Services;

public class AudioServiceTests
{
    // Real SoundVolumeView /scomma column order (0-indexed):
    // [0] Name, [1] Type, [2] Direction, [3] Device Name,
    // [4] Default (Console), [5] Default Multimedia, [6] Default Communications,
    // [7] Device State, [8] Muted, [9] Volume dB, [10] Volume Percent, ...
    private const string SampleCsv =
        """
        Speakers,Device,Render,Realtek High Definition Audio,Render,Render,,Active,No,-10.50,50.0%,0,0,0,0,,,,{guid},,,,
        Headphones,Device,Render,Realtek High Definition Audio,,,,Active,No,-6.00,75.5%,0,0,0,0,,,,{guid2},,,,
        Microphone,Device,Capture,Realtek High Definition Audio,Capture,Capture,,Active,No,-3.00,80.0%,0,0,0,0,,,,{guid3},,,,
        """;

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
        var csv = "Speakers,Realtek,{guid}";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.ShouldBeEmpty();
    }

    [Fact]
    public void ParseCsvOutput_CaptureDevicesExcluded()
    {
        var csv = "Microphone,Device,Capture,Realtek High Definition Audio,Capture,Capture,,Active,No,-3.00,100.0%,0,0,0,0,,,,{guid},,,,";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.ShouldBeEmpty();
    }

    [Fact]
    public void ParseCsvOutput_NoDefaultDevice_AllIsDefaultFalse()
    {
        var csv = "Speakers,Device,Render,Realtek High Definition Audio,,,,Active,No,-10.50,50.0%,0,0,0,0,,,,{guid},,,,";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.Count.ShouldBe(1);
        devices[0].IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void ParseCsvOutput_VolumeWithoutPercent_ParsesAsZero()
    {
        var csv = "Speakers,Device,Render,Realtek High Definition Audio,Render,Render,,Active,No,-10.50,invalid,0,0,0,0,,,,{guid},,,,";
        var devices = AudioService.ParseCsvOutput(csv);

        devices[0].Volume.ShouldBe(0);
    }

    [Fact]
    public void ParseCsvOutput_QuotedFieldsWithCommas_ParsedCorrectly()
    {
        var csv = "\"Speakers, Front\",Device,Render,Realtek High Definition Audio,Render,Render,,Active,No,-10.50,50.0%,0,0,0,0,,,,{guid},,,,";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.Count.ShouldBe(1);
        devices[0].Name.ShouldBe("Speakers, Front");
    }

    [Fact]
    public void ParseCsvOutput_WindowsLineEndings_ParsedCorrectly()
    {
        var csv = "Speakers,Device,Render,Realtek High Definition Audio,Render,Render,,Active,No,-10.50,50.0%,0,0,0,0,,,,{guid},,,,\r\nHeadphones,Device,Render,Realtek High Definition Audio,,,,Active,No,-6.00,75.0%,0,0,0,0,,,,{guid2},,,,\r\n";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.Count.ShouldBe(2);
    }
}
