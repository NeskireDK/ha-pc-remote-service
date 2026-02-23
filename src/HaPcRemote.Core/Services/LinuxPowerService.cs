using System.Diagnostics;
using System.Runtime.Versioning;

namespace HaPcRemote.Service.Services;

[SupportedOSPlatform("linux")]
public sealed class LinuxPowerService(ILogger<LinuxPowerService> logger) : IPowerService
{
    public async Task SleepAsync()
    {
        logger.LogInformation("Suspending system");

        // loginctl works in user sessions with systemd (no sudo required)
        if (await RunCommandAsync("loginctl", "suspend"))
            return;

        // Fallback to systemctl suspend
        await RunCommandAsync("systemctl", "suspend");
    }

    private async Task<bool> RunCommandAsync(string command, string args)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Command {Command} {Args} failed", command, args);
            return false;
        }
    }
}
