namespace HaPcRemote.Service.Models;

internal sealed class LinuxMonitorProfileData
{
    public List<LinuxMonitorOutputConfig> Outputs { get; set; } = [];
}

internal sealed class LinuxMonitorOutputConfig
{
    public string Name { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public int RefreshRate { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsEnabled { get; set; }
}
