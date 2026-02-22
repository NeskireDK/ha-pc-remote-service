using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace HaPcRemote.Shared.Ipc;

/// <summary>
/// Handles IPC requests by dispatching to the appropriate handler method.
/// Extracted from TrayApplicationContext so process execution logic is testable.
/// </summary>
public sealed class IpcRequestHandler
{
    private readonly ILogger _logger;

    public IpcRequestHandler(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        return request.Type switch
        {
            "ping" => IpcResponse.Ok(),
            "runCli" => await HandleRunCliAsync(request, ct),
            "launchProcess" => HandleLaunchProcess(request),
            _ => IpcResponse.Fail($"Unknown request type: {request.Type}")
        };
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
