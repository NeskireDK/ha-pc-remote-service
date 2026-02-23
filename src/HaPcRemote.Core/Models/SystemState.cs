namespace HaPcRemote.Service.Models;

public sealed class SystemState
{
    public AudioState? Audio { get; init; }
    public List<MonitorInfo>? Monitors { get; init; }
    public List<string>? MonitorProfiles { get; init; }
    public List<SteamGame>? SteamGames { get; init; }
    public SteamGame? RunningGame { get; init; }
    public List<string>? Modes { get; init; }
}

public sealed class AudioState
{
    public required List<string> Devices { get; init; }
    public string? Current { get; init; }
    public int? Volume { get; init; }
}
