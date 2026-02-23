using System.Diagnostics;

namespace HaPcRemote.Service.Services;

/// <summary>
/// Launches processes directly in the current user session.
/// Used by the Tray host where no IPC is needed.
/// </summary>
public sealed class DirectAppLauncher : IAppLauncher
{
    public Task LaunchAsync(string exePath, string? arguments = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (!string.IsNullOrEmpty(arguments))
            startInfo.Arguments = arguments;
        Process.Start(startInfo);
        return Task.CompletedTask;
    }
}
