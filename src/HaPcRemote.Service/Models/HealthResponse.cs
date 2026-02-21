namespace HaPcRemote.Service.Models;

public sealed class HealthResponse
{
    public required string Status { get; init; }
    public string? MachineName { get; init; }
    public List<MacAddressInfo>? MacAddresses { get; init; }
}
