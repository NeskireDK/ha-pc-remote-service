namespace HaPcRemote.Service.Models;

public sealed class SteamRunningGame
{
    public required int AppId { get; init; }
    public required string Name { get; init; }
}
