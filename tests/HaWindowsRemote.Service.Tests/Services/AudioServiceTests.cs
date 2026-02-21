using HaWindowsRemote.Service.Services;
using Shouldly;

namespace HaWindowsRemote.Service.Tests.Services;

public class AudioServiceTests
{
    private const string SampleCsv =
        """
        Speakers,Realtek Audio,{guid},Render,,Render,Yes,50.0%,0,,
        Headphones,Realtek Audio,{guid2},Render,,,No,75.5%,0,,
        Microphone,Realtek Audio,{guid3},Capture,,Capture,Yes,80.0%,0,,
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
        var csv = "Microphone,Realtek Audio,{guid},Capture,,Capture,Yes,100.0%,0,,";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.ShouldBeEmpty();
    }

    [Fact]
    public void ParseCsvOutput_NoDefaultDevice_AllIsDefaultFalse()
    {
        var csv = "Speakers,Realtek Audio,{guid},Render,,,No,50.0%,0,,";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.Count.ShouldBe(1);
        devices[0].IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void ParseCsvOutput_VolumeWithoutPercent_ParsesAsZero()
    {
        var csv = "Speakers,Realtek Audio,{guid},Render,,Render,Yes,invalid,0,,";
        var devices = AudioService.ParseCsvOutput(csv);

        devices[0].Volume.ShouldBe(0);
    }

    [Fact]
    public void ParseCsvOutput_QuotedFieldsWithCommas_ParsedCorrectly()
    {
        var csv = "\"Speakers, Front\",Realtek Audio,{guid},Render,,Render,Yes,50.0%,0,,";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.Count.ShouldBe(1);
        devices[0].Name.ShouldBe("Speakers, Front");
    }

    [Fact]
    public void ParseCsvOutput_WindowsLineEndings_ParsedCorrectly()
    {
        var csv = "Speakers,Realtek Audio,{guid},Render,,Render,Yes,50.0%,0,,\r\nHeadphones,Realtek Audio,{guid2},Render,,,No,75.0%,0,,\r\n";
        var devices = AudioService.ParseCsvOutput(csv);

        devices.Count.ShouldBe(2);
    }
}
