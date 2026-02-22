using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace HaPcRemote.Shared.Ipc;

/// <summary>
/// Handles IPC requests by dispatching to the appropriate handler method.
/// Extracted from TrayApplicationContext so process execution logic is testable.
/// </summary>
public sealed class IpcRequestHandler(ILogger logger)
{

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        logger.LogDebug("IPC ← {Type}", request.Type);

        var response = request.Type switch
        {
            "ping" => IpcResponse.Ok(),
            "runCli" => await HandleRunCliAsync(request, ct),
            "launchProcess" => HandleLaunchProcess(request),
            "steamGetPath" => HandleSteamGetPath(),
            "steamGetRunningId" => HandleSteamGetRunningId(),
            "steamLaunchUrl" => HandleSteamLaunchUrl(request),
            "steamKillDir" => HandleSteamKillDir(request),
            _ => IpcResponse.Fail($"Unknown request type: {request.Type}")
        };

        if (response.Success)
        {
            // Truncate stdout — runCli can return hundreds of KB from tools like SoundVolumeView
            var preview = response.Stdout is { Length: > 120 }
                ? response.Stdout[..120] + "…"
                : response.Stdout;
            logger.LogDebug("IPC → {Type} ok stdout={Stdout}", request.Type, preview);
        }
        else
            logger.LogDebug("IPC → {Type} FAIL {Error}", request.Type, response.Error);

        return response;
    }

    private async Task<IpcResponse> HandleRunCliAsync(IpcRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.ExePath))
            return IpcResponse.Fail("ExePath is required for runCli");

        if (!File.Exists(request.ExePath))
            return IpcResponse.Fail($"CLI tool not found: {request.ExePath}");

        var timeout = TimeSpan.FromMilliseconds(request.TimeoutMs);

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = request.ExePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (request.Arguments is not null)
        {
            foreach (var arg in request.Arguments)
                process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();

        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                return IpcResponse.Fail(
                    $"Process '{request.ExePath}' timed out after {timeout.TotalSeconds}s.");
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                return new IpcResponse
                {
                    Success = false,
                    Error = $"Process exited with code {process.ExitCode}: {stderr}",
                    Stdout = stdout,
                    Stderr = stderr,
                    ExitCode = process.ExitCode
                };
            }

            return IpcResponse.Ok(stdout, stderr, process.ExitCode);
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
    }

    private IpcResponse HandleSteamGetPath()
    {
        if (!OperatingSystem.IsWindows())
            return IpcResponse.Fail("Steam commands require Windows");

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
            return IpcResponse.Ok(key?.GetValue("SteamPath") as string);
        }
        catch (Exception ex)
        {
            return IpcResponse.Fail($"Failed to read Steam path: {ex.Message}");
        }
    }

    private IpcResponse HandleSteamGetRunningId()
    {
        if (!OperatingSystem.IsWindows())
            return IpcResponse.Fail("Steam commands require Windows");

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
            var appId = key?.GetValue("RunningAppID") is int id ? id : 0;
            return IpcResponse.Ok(appId.ToString());
        }
        catch (Exception ex)
        {
            return IpcResponse.Fail($"Failed to read Steam running app ID: {ex.Message}");
        }
    }

    private IpcResponse HandleSteamLaunchUrl(IpcRequest request)
    {
        if (string.IsNullOrEmpty(request.ProcessArguments))
            return IpcResponse.Fail("ProcessArguments (URL) is required for steamLaunchUrl");

        try
        {
            Process.Start(new ProcessStartInfo(request.ProcessArguments) { UseShellExecute = true });
            return IpcResponse.Ok();
        }
        catch (Exception ex)
        {
            return IpcResponse.Fail($"Failed to launch Steam URL: {ex.Message}");
        }
    }

    private IpcResponse HandleSteamKillDir(IpcRequest request)
    {
        if (string.IsNullOrEmpty(request.ProcessArguments))
            return IpcResponse.Fail("ProcessArguments (directory) is required for steamKillDir");

        var directory = request.ProcessArguments;
        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                var path = proc.MainModule?.FileName;
                if (path != null && path.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
                    proc.Kill(entireProcessTree: true);
            }
            catch
            {
                // Access denied for system processes, or process already exited
            }
            finally
            {
                proc.Dispose();
            }
        }

        return IpcResponse.Ok();
    }

    private IpcResponse HandleLaunchProcess(IpcRequest request)
    {
        if (string.IsNullOrEmpty(request.ExePath))
            return IpcResponse.Fail("ExePath is required for launchProcess");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = request.ExePath,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (!string.IsNullOrEmpty(request.ProcessArguments))
                startInfo.Arguments = request.ProcessArguments;

            Process.Start(startInfo);
            return IpcResponse.Ok();
        }
        catch (Exception ex)
        {
            return IpcResponse.Fail($"Failed to launch process: {ex.Message}");
        }
    }
}
