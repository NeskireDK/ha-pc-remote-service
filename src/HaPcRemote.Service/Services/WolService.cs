using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using HaPcRemote.Service.Models;

namespace HaPcRemote.Service.Services;

public static class WolService
{
    private const int WolPort = 9;

    /// <summary>
    /// Returns MAC and IP addresses for all active, non-loopback network interfaces.
    /// </summary>
    public static List<MacAddressInfo> GetMacAddresses()
    {
        var result = new List<MacAddressInfo>();

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback
                or NetworkInterfaceType.Tunnel) continue;

            var macBytes = ni.GetPhysicalAddress().GetAddressBytes();
            if (macBytes.Length == 0 || Array.TrueForAll(macBytes, b => b == 0)) continue;

            var macStr = BitConverter.ToString(macBytes).Replace('-', ':');

            var ipAddress = string.Empty;
            foreach (var addr in ni.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    ipAddress = addr.Address.ToString();
                    break;
                }
            }

            result.Add(new MacAddressInfo
            {
                InterfaceName = ni.Name,
                MacAddress = macStr,
                IpAddress = ipAddress
            });
        }

        return result;
    }

    /// <summary>
    /// Sends a Wake-on-LAN magic packet to the specified MAC address.
    /// The magic packet is a UDP broadcast containing 6 bytes of 0xFF
    /// followed by the target MAC address repeated 16 times.
    /// </summary>
    public static async Task SendWolAsync(string macAddress)
    {
        var macBytes = ParseMacAddress(macAddress);
        var magicPacket = BuildMagicPacket(macBytes);

        using var client = new UdpClient();
        client.EnableBroadcast = true;
        await client.SendAsync(magicPacket, magicPacket.Length,
            new IPEndPoint(IPAddress.Broadcast, WolPort));
    }

    internal static byte[] ParseMacAddress(string macAddress)
    {
        var hex = macAddress.Replace(":", "").Replace("-", "");
        if (hex.Length != 12)
            throw new ArgumentException($"Invalid MAC address format: {macAddress}");

        var bytes = new byte[6];
        for (var i = 0; i < 6; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);

        return bytes;
    }

    internal static byte[] BuildMagicPacket(byte[] macBytes)
    {
        // Magic packet: 6 bytes of 0xFF + MAC address repeated 16 times = 102 bytes
        var packet = new byte[6 + 16 * 6];

        for (var i = 0; i < 6; i++)
            packet[i] = 0xFF;

        for (var i = 0; i < 16; i++)
            Buffer.BlockCopy(macBytes, 0, packet, 6 + i * 6, 6);

        return packet;
    }
}
