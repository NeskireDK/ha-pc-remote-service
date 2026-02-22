using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace HaPcRemote.Tray.Services;

/// <summary>
/// Restarts the HaPcRemoteService Windows Service via an elevated PowerShell command.
/// Single UAC prompt for stop + start.
/// </summary>
internal static class ServiceController
{
    private const string ServiceName = "HaPcRemoteService";

    public static async Task RestartAsync(ILogger logger, CancellationToken ct = default)
    {
        logger.LogInformation("Restarting service {ServiceName}...", ServiceName);

        var cmd = $"Stop-Service -Name '{ServiceName}' -Force; Start-Sleep -Seconds 2; Start-Service -Name '{ServiceName}'";
        var result = await RunElevatedAsync(cmd, logger, ct);

        if (result)
            logger.LogInformation("Service restarted successfully");
        else
            logger.LogError("Failed to restart service");
    }

    public static async Task<bool> StopAsync(ILogger logger, CancellationToken ct = default)
    {
        logger.LogInformation("Stopping service {ServiceName}...", ServiceName);

        var cmd = $"Stop-Service -Name '{ServiceName}' -Force";
        var result = await RunElevatedAsync(cmd, logger, ct);

        if (result)
            logger.LogInformation("Service stopped successfully");
        else
            logger.LogWarning("Failed to stop service");

        return result;
    }

    private static async Task<bool> RunElevatedAsync(string command, ILogger logger, CancellationToken ct)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (process is null)
            {
                logger.LogError("Failed to start elevated PowerShell process");
                return false;
            }

            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // User cancelled UAC prompt
            logger.LogWarning("UAC prompt was cancelled");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute elevated command");
            return false;
        }
    }
}
