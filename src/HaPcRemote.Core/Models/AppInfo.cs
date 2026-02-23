namespace HaPcRemote.Service.Models;

public sealed class AppInfo
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required bool IsRunning { get; init; }
}
