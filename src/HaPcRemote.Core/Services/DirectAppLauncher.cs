using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace HaPcRemote.Service.Services;

/// <summary>
/// Launches processes directly in the current user session.
/// Used by the Tray host where no IPC is needed.
/// </summary>
public sealed class DirectAppLauncher(ILogger<DirectAppLauncher> logger) : IAppLauncher
{
    public Task LaunchAsync(string exePath, string? arguments = null, bool useShellExecute = false)
    {
        // Protocol URIs (steam://, http://, etc.) require ShellExecute — without it,
        // .NET resolves the URI as a relative file path and fails.
        if (exePath.Contains("://"))
            useShellExecute = true;

        logger.LogInformation("Launching: {ExePath} {Args} (shell={Shell})", exePath, arguments ?? "", useShellExecute);

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = useShellExecute,
            CreateNoWindow = !useShellExecute
        };
        if (!string.IsNullOrEmpty(arguments))
            startInfo.Arguments = arguments;

        try
        {
            using var process = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to launch '{ExePath}' (shell={Shell})", exePath, useShellExecute);
            throw;
        }

        return Task.CompletedTask;
    }
}
