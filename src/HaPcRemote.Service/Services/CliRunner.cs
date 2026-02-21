using System.Diagnostics;

namespace HaPcRemote.Service.Services;

public static class CliRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    public static async Task<string> RunAsync(string exePath, string arguments, TimeSpan? timeout = null)
    {
        if (!File.Exists(exePath))
            throw new FileNotFoundException($"CLI tool not found: {exePath}", exePath);

        var effectiveTimeout = timeout ?? DefaultTimeout;

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();

        // Read stdout and stderr concurrently to avoid deadlocks
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(effectiveTimeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"Process '{exePath}' timed out after {effectiveTimeout.TotalSeconds}s.");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Process '{exePath}' exited with code {process.ExitCode}: {stderr}");
        }

        return stdout;
    }
}
