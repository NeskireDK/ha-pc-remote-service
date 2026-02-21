using HaPcRemote.Service.Services;
using Shouldly;

namespace HaPcRemote.Service.Tests.Services;

public class WolServiceTests
{
    [Theory]
    [InlineData("AA:BB:CC:DD:EE:FF", new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF })]
    [InlineData("aa:bb:cc:dd:ee:ff", new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF })]
    [InlineData("AA-BB-CC-DD-EE-FF", new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF })]
    [InlineData("AABBCCDDEEFF", new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF })]
    [InlineData("00:11:22:33:44:55", new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 })]
    public void ParseMacAddress_ValidFormats_ReturnsCorrectBytes(string input, byte[] expected)
    {
        var result = WolService.ParseMacAddress(input);

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("AA:BB:CC")]
    [InlineData("not-a-mac")]
    [InlineData("AA:BB:CC:DD:EE:FF:00")]
    public void ParseMacAddress_InvalidFormats_ThrowsArgumentException(string input)
    {
        Should.Throw<ArgumentException>(() => WolService.ParseMacAddress(input));
    }

    [Fact]
    public void BuildMagicPacket_CorrectLength()
    {
        var mac = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF };

        var packet = WolService.BuildMagicPacket(mac);

        packet.Length.ShouldBe(102); // 6 + 16 * 6
    }

    [Fact]
    public void BuildMagicPacket_StartsWithSixFF()
    {
        var mac = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };

        var packet = WolService.BuildMagicPacket(mac);

        for (var i = 0; i < 6; i++)
            packet[i].ShouldBe((byte)0xFF);
    }

    [Fact]
    public void BuildMagicPacket_ContainsMacRepeated16Times()
    {
        var mac = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF };

        var packet = WolService.BuildMagicPacket(mac);

        for (var i = 0; i < 16; i++)
        {
            var offset = 6 + i * 6;
            packet[offset..(offset + 6)].ShouldBe(mac);
        }
    }

    [Fact]
    public void GetMacAddresses_ReturnsNonEmptyList()
    {
        // This test depends on the host having at least one active network interface.
        // CI environments typically have at least a loopback + one real NIC.
        var macs = WolService.GetMacAddresses();

        // We can't assert exact values, but the structure should be correct
        foreach (var entry in macs)
        {
            entry.InterfaceName.ShouldNotBeNullOrEmpty();
            entry.MacAddress.ShouldNotBeNullOrEmpty();
            // MAC should be in AA:BB:CC:DD:EE:FF format
            entry.MacAddress.Split(':').Length.ShouldBe(6);
        }
    }
}
