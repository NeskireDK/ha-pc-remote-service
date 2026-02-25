namespace HaPcRemote.Service.Models;

public sealed class SteamGame
{
    public required int AppId { get; init; }
    public required string Name { get; init; }
    public required long LastPlayed { get; init; }
    public bool IsShortcut { get; init; }
}
