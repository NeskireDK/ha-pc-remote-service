namespace HaPcRemote.Service.Services;

/// <summary>
/// Abstraction for launching processes. The tray implementation
/// launches in the interactive user session; the fallback launches directly.
/// </summary>
public interface IAppLauncher
{
    Task LaunchAsync(string exePath, string? arguments = null);
}
