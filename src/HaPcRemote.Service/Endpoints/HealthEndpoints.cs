using System.Net.NetworkInformation;
using System.Net.Sockets;
using HaPcRemote.Service.Models;

namespace HaPcRemote.Service.Endpoints;

public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api");

        group.MapGet("/health", () =>
        {
            var response = ApiResponse.Ok(new HealthResponse
            {
                Status = "ok",
                MachineName = Environment.MachineName,
                MacAddresses = GetMacAddresses()
            });
            return Results.Json(response, AppJsonContext.Default.ApiResponseHealthResponse);
        });

        return group;
    }

    private static List<MacAddressInfo> GetMacAddresses()
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
}
