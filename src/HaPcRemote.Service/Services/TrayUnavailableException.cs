namespace HaPcRemote.Service.Services;

/// <summary>
/// Thrown when a tray IPC call cannot reach the tray app (tray not running).
/// Callers (e.g. endpoints) should return 503 Service Unavailable in this case.
/// </summary>
public sealed class TrayUnavailableException(string message) : Exception(message);
