namespace HaPcRemote.Shared.Ipc;

/// <summary>
/// Request message sent from the service to the tray app.
/// The Type field determines which properties are relevant.
/// </summary>
public sealed class IpcRequest
{
    /// <summary>
    /// Message type: "runCli", "launchProcess", "ping",
    /// "steamGetPath", "steamGetRunningId", "steamLaunchUrl", or "steamKillDir".
    /// </summary>
    public required string Type { get; init; }

    /// <summary>Path to the executable (runCli, launchProcess).</summary>
    public string? ExePath { get; init; }

    /// <summary>CLI arguments array (runCli).</summary>
    public string[]? Arguments { get; init; }

    /// <summary>
    /// Process arguments string (launchProcess).
    /// Also used as the steam:// URL (steamLaunchUrl) or directory path (steamKillDir).
    /// </summary>
    public string? ProcessArguments { get; init; }

    /// <summary>Timeout in milliseconds (runCli). Default 10000.</summary>
    public int TimeoutMs { get; init; } = 10000;
}

/// <summary>
/// Response message sent from the tray app to the service.
/// </summary>
public sealed class IpcResponse
{
    public required bool Success { get; init; }
    public string? Error { get; init; }

    /// <summary>Standard output from the CLI tool (runCli).</summary>
    public string? Stdout { get; init; }

    /// <summary>Standard error from the CLI tool (runCli).</summary>
    public string? Stderr { get; init; }

    /// <summary>Exit code from the CLI tool (runCli).</summary>
    public int ExitCode { get; init; }

    public static IpcResponse Ok(string? stdout = null, string? stderr = null, int exitCode = 0) =>
        new() { Success = true, Stdout = stdout, Stderr = stderr, ExitCode = exitCode };

    public static IpcResponse Fail(string error) =>
        new() { Success = false, Error = error };
}
