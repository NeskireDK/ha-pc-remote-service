namespace HaPcRemote.Service.Models;

public sealed class MacAddressInfo
{
    public required string InterfaceName { get; init; }
    public required string MacAddress { get; init; }
    public required string IpAddress { get; init; }
}
