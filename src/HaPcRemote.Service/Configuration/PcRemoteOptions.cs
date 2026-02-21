namespace HaPcRemote.Service.Configuration;

public sealed class PcRemoteOptions
{
    public const string SectionName = "PcRemote";

    public int Port { get; set; } = 5000;
    public AuthOptions Auth { get; set; } = new();
    public string ToolsPath { get; set; } = "./tools";
    public string ProfilesPath { get; set; } = "./monitor-profiles";
    public Dictionary<string, AppDefinitionOptions> Apps { get; set; } = new();
}

public sealed class AuthOptions
{
    public bool Enabled { get; set; } = true;
    public string ApiKey { get; set; } = string.Empty;
}
