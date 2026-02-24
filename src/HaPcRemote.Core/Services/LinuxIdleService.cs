using System.Diagnostics;
using System.Runtime.Versioning;

namespace HaPcRemote.Service.Services;

[SupportedOSPlatform("linux")]
public sealed class LinuxIdleService(ILogger<LinuxIdleService> logger) : IIdleService
{
    public int? GetIdleSeconds()
    {
        // xprintidle returns milliseconds since last X11 input event
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "xprintidle",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });

            if (process is null) return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            if (process.ExitCode == 0 && int.TryParse(output, out var ms))
                return ms / 1000;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "xprintidle not available");
        }

        return null;
    }
}
