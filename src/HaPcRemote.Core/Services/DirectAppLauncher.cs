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
        // Also detect "scheme:path" (no double slash) — e.g. "steam:\open\bigpicture"
        // from stale config values where the URI was mangled.
        if (IsProtocolUri(exePath))
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

    /// <summary>
    /// Detects protocol URIs: "steam://open/bigpicture", "http://...",
    /// and malformed variants like "steam:\open\bigpicture" from stale configs.
    /// </summary>
    internal static bool IsProtocolUri(string path)
    {
        // Standard "scheme://path"
        if (path.Contains("://"))
            return true;

        // Malformed "scheme:\path" or "scheme:path" — look for a short alphabetic prefix before ':'
        var colon = path.IndexOf(':');
        if (colon > 1 && colon <= 10)
        {
            // Drive letters are single char (C:), schemes are 2+ chars (steam:, http:)
            for (var i = 0; i < colon; i++)
            {
                if (!char.IsLetter(path[i]))
                    return false;
            }
            return true;
        }

        return false;
    }
}
