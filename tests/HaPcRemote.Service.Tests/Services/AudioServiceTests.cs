using HaPcRemote.Service.Services;
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
}
