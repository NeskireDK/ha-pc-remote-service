using System.Diagnostics;
using System.Runtime.Versioning;

namespace HaPcRemote.Service.Services;

[SupportedOSPlatform("linux")]
public sealed class LinuxIdleService(ILogger<LinuxIdleService> logger) : IIdleService
{
    private IdleBackend? _backend;
    private bool _backendResolved;

    public int? GetIdleSeconds()
    {
        if (!_backendResolved)
        {
            _backend = DetectBackend();
            _backendResolved = true;
            logger.LogInformation("Idle detection backend: {Backend}", _backend?.GetType().Name ?? "none");
        }

        return _backend?.GetIdleSeconds();
    }

    private IdleBackend? DetectBackend()
    {
        if (IsGnome())
        {
            try
            {
                var backend = new MutterIdleBackend(logger);
                var test = backend.GetIdleSeconds();
                if (test is not null)
                    return backend;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Mutter IdleMonitor not available");
            }
        }

        if (IsX11())
        {
            var backend = new XprintidleBackend(logger);
            var test = backend.GetIdleSeconds();
            if (test is not null)
                return backend;
        }

        try
        {
            var backend = new LogindIdleBackend(logger);
            var test = backend.GetIdleSeconds();
            if (test is not null)
                return backend;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "logind idle hint not available");
        }

        logger.LogWarning("No idle detection backend available");
        return null;
    }

    internal static bool IsGnome() =>
        Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")
            ?.Contains("GNOME", StringComparison.OrdinalIgnoreCase) == true;

    internal static bool IsX11() =>
        Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")
            ?.Equals("x11", StringComparison.OrdinalIgnoreCase) == true;
}

internal abstract class IdleBackend
{
    public abstract int? GetIdleSeconds();
}

/// <summary>
/// GNOME Mutter IdleMonitor via gdbus CLI.
/// Calls org.gnome.Mutter.IdleMonitor.GetIdletime() which returns uint64 milliseconds.
/// Works on both X11 and Wayland GNOME sessions.
/// </summary>
internal sealed class MutterIdleBackend(ILogger logger) : IdleBackend
{
    public override int? GetIdleSeconds()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "gdbus",
                ArgumentList =
                {
                    "call", "--session",
                    "--dest", "org.gnome.Mutter.IdleMonitor",
                    "--object-path", "/org/gnome/Mutter/IdleMonitor/Core",
                    "--method", "org.gnome.Mutter.IdleMonitor.GetIdletime"
                },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });

            if (process is null) return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            if (process.ExitCode != 0) return null;

            // gdbus returns "(uint64 12345,)" — extract the number
            return ParseGdbusUInt64(output) is { } ms ? (int)(ms / 1000) : null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Mutter GetIdletime failed");
            return null;
        }
    }

    internal static ulong? ParseGdbusUInt64(string output)
    {
        // Format: "(uint64 12345,)" or "(12345,)"
        var start = output.LastIndexOf(' ');
        if (start < 0) start = output.IndexOf('(');
        if (start < 0) return null;

        var end = output.IndexOf(',', start);
        if (end < 0) end = output.IndexOf(')', start);
        if (end < 0) return null;

        var numberStr = output[(start + 1)..end].Trim();
        return ulong.TryParse(numberStr, out var value) ? value : null;
    }
}

/// <summary>
/// logind session idle detection via gdbus CLI.
/// Reads IdleHint (bool) and IdleSinceHintMonotonic (uint64 microseconds) from
/// /org/freedesktop/login1/session/auto.
/// </summary>
internal sealed class LogindIdleBackend(ILogger logger) : IdleBackend
{
    public override int? GetIdleSeconds()
    {
        try
        {
            var idleHint = GetProperty("IdleHint");
            if (idleHint is null) return null;

            // "(true,)" or "(false,)"
            if (!ParseGdbusBool(idleHint, out var isIdle)) return null;
            if (!isIdle) return 0;

            var sinceHint = GetProperty("IdleSinceHintMonotonic");
            if (sinceHint is null) return null;

            if (ParseGdbusVariantUInt64(sinceHint) is not { } idleSinceUs)
                return null;

            // Compare against monotonic clock
            var nowUs = GetMonotonicTimeUs();
            if (nowUs is null || idleSinceUs == 0) return null;

            var elapsedUs = nowUs.Value - (long)idleSinceUs;
            return elapsedUs > 0 ? (int)(elapsedUs / 1_000_000) : 0;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "logind idle detection failed");
            return null;
        }
    }

    private string? GetProperty(string propertyName)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "gdbus",
            ArgumentList =
            {
                "call", "--system",
                "--dest", "org.freedesktop.login1",
                "--object-path", "/org/freedesktop/login1/session/auto",
                "--method", "org.freedesktop.DBus.Properties.Get",
                "org.freedesktop.login1.Session", propertyName
            },
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        });

        if (process is null) return null;

        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();

        return process.ExitCode == 0 ? output : null;
    }

    internal static bool ParseGdbusBool(string output, out bool value)
    {
        // Format: "(<true>,)" or "(<false>,)"
        value = false;
        if (output.Contains("true", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }
        if (output.Contains("false", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }
        return false;
    }

    internal static ulong? ParseGdbusVariantUInt64(string output)
    {
        // Format: "(<uint64 12345>,)" — variant wrapper with angle brackets
        var start = output.LastIndexOf(' ');
        if (start < 0) return null;

        // Find the number end — look for '>' or ',' or ')'
        var numberStr = output[(start + 1)..].TrimEnd('>', ',', ')', ' ');
        return ulong.TryParse(numberStr, out var value) ? value : null;
    }

    internal static long? GetMonotonicTimeUs()
    {
        try
        {
            // /proc/timer_list is not always accessible; use clock_gettime via /proc/uptime
            // Actually, Stopwatch.GetTimestamp uses CLOCK_MONOTONIC on Linux
            var ticks = Stopwatch.GetTimestamp();
            var frequency = Stopwatch.Frequency;
            return ticks * 1_000_000 / frequency;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// xprintidle CLI for X11 sessions. Returns milliseconds since last X11 input event.
/// </summary>
internal sealed class XprintidleBackend(ILogger logger) : IdleBackend
{
    public override int? GetIdleSeconds()
    {
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

            if (process.ExitCode == 0 && long.TryParse(output, out var ms))
                return (int)(ms / 1000);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "xprintidle not available");
        }

        return null;
    }
}
