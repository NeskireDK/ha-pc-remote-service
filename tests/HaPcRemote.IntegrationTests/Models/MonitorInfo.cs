namespace HaPcRemote.IntegrationTests.Models;

public class MonitorInfo
{
    public string Name { get; set; } = string.Empty;
    public string MonitorId { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string MonitorName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int DisplayFrequency { get; set; }
    public bool IsActive { get; set; }
    public bool IsPrimary { get; set; }
}
