namespace HaPcRemote.Service.Models;

public sealed class RunningGameDiagnostics
{
    public required int SteamReportedAppId { get; init; }
    public required bool SteamRunning { get; init; }
    public required int ShortcutsChecked { get; init; }
    public required int RunningProcessCount { get; init; }
    public required List<ShortcutDetectionTrace> Traces { get; init; }
    public SteamRunningGame? Result { get; init; }
}

public sealed class ShortcutDetectionTrace
{
    public required int AppId { get; init; }
    public required string Name { get; init; }
    public required string? ExePath { get; init; }
    public required string? LaunchOptions { get; init; }
    public required List<RunningProcess> FilenameMatches { get; init; }
    public required bool ExactPathMatch { get; init; }
    public required bool Matched { get; init; }
    public int? MatchedPid { get; init; }
    public string? MatchReason { get; init; }
}
