namespace HaPcRemote.Service.Models;

public sealed class SteamGame
{
    public required int AppId { get; init; }
    public required string Name { get; init; }
    public required long LastPlayed { get; init; }
    public bool IsShortcut { get; init; }
    public string? ExePath { get; init; }
    public string? LaunchOptions { get; init; }
}

public sealed record RunningProcess(int Pid, string Path, string? CommandLine);
