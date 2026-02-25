namespace HaPcRemote.Service.Models;

public sealed class SteamBindings
{
    public string DefaultPcMode { get; set; } = string.Empty;
    public Dictionary<string, string> GamePcModeBindings { get; set; } = new();
}
