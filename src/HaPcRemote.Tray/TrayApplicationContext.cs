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
    private readonly IpcRequestHandler _requestHandler;
    private readonly IpcServer _ipcServer;

    public TrayApplicationContext()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        _logger = _loggerFactory.CreateLogger<TrayApplicationContext>();

        _requestHandler = new IpcRequestHandler(_logger);
        _ipcServer = new IpcServer(_requestHandler.HandleAsync, _logger);

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
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

    private static Icon LoadAppIcon()
    {
        return Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application;
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
