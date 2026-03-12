using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace HaPcRemote.IntegrationTests;

public class WakeTests
{
    [Fact]
    public async Task WakePC_SendsWolPackets()
    {
        var mac = PhysicalAddress.Parse("BC-FC-E7-6A-90-2E");
        var macBytes = mac.GetAddressBytes();
        var packet = new byte[102];
        for (var i = 0; i < 6; i++) packet[i] = 0xFF;
        for (var i = 0; i < 16; i++) Buffer.BlockCopy(macBytes, 0, packet, 6 + i * 6, 6);

        for (var i = 0; i < 5; i++)
        {
            using var udp = new UdpClient();
            udp.EnableBroadcast = true;
            await udp.SendAsync(packet, packet.Length, "255.255.255.255", 9);
            await Task.Delay(1000);
        }
    }
}
