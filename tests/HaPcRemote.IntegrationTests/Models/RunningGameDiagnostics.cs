namespace HaPcRemote.IntegrationTests.Models;

public class RunningGameDiagnostics
{
    public int SteamReportedAppId { get; set; }
    public bool SteamRunning { get; set; }
    public int ShortcutsChecked { get; set; }
    public int RunningProcessCount { get; set; }
    public List<ShortcutDetectionTrace> Traces { get; set; } = [];
    public SteamRunningGame? Result { get; set; }
}

public class ShortcutDetectionTrace
{
    public int AppId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ExePath { get; set; }
    public string? LaunchOptions { get; set; }
    public List<ProcessMatch> FilenameMatches { get; set; } = [];
    public bool ExactPathMatch { get; set; }
    public bool Matched { get; set; }
    public int? MatchedPid { get; set; }
    public string? MatchReason { get; set; }
}

public class ProcessMatch
{
    public int Pid { get; set; }
    public string Path { get; set; } = string.Empty;
    public string? CommandLine { get; set; }
}
