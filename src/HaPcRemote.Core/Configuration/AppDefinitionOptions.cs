namespace HaPcRemote.Service.Configuration;

public sealed class AppDefinitionOptions
{
    public string DisplayName { get; set; } = string.Empty;
    public string ExePath { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public string ProcessName { get; set; } = string.Empty;
}
