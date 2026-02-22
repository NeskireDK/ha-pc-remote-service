using System.Diagnostics;
using HaPcRemote.Shared.Ipc;
using Microsoft.Extensions.Logging;

namespace HaPcRemote.Tray;

/// <summary>
/// WinForms application context that shows a system tray icon
/// and hosts the named pipe IPC server.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly CancellationTokenSource _cts = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly IpcServer _ipcServer;

    public TrayApplicationContext()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        _logger = _loggerFactory.CreateLogger<TrayApplicationContext>();

        _ipcServer = new IpcServer(HandleRequestAsync, _logger);

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadEmbeddedIcon(),
            Text = "HA PC Remote",
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };

        _ = Task.Run(() => RunIpcServerAsync(_cts.Token));
        _logger.LogInformation("HA PC Remote Tray started");
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("HA PC Remote", null, null!).Enabled = false;
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, OnExit);
        return menu;
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _cts.Cancel();
        _notifyIcon.Visible = false;
        Application.Exit();
    }

    private async Task RunIpcServerAsync(CancellationToken ct)
    {
        try
        {
            await _ipcServer.RunAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IPC server crashed");
        }
    }

    private async Task<IpcResponse> HandleRequestAsync(IpcRequest request, CancellationToken ct)
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

    private static Icon LoadEmbeddedIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        return File.Exists(iconPath)
            ? new Icon(iconPath)
            : SystemIcons.Application;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Cancel();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _loggerFactory.Dispose();
            _cts.Dispose();
        }
        base.Dispose(disposing);
    }
}
