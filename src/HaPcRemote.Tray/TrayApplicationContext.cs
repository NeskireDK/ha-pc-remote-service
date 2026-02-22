using HaPcRemote.Shared.Ipc;
using HaPcRemote.Tray.Forms;
using HaPcRemote.Tray.Logging;
using HaPcRemote.Tray.Services;
using Microsoft.Extensions.Logging;

namespace HaPcRemote.Tray;

/// <summary>
/// WinForms application context that shows a system tray icon
/// and hosts the named pipe IPC server.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly string VersionString = GetVersionString();

    private readonly NotifyIcon _notifyIcon;
    private readonly CancellationTokenSource _cts = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly IpcRequestHandler _requestHandler;
    private readonly IpcServer _ipcServer;
    private readonly InMemoryLogProvider _logProvider;
    private readonly UpdateChecker _updateChecker;
    private readonly System.Windows.Forms.Timer _updateTimer;

    private LogViewerForm? _logViewerForm;
    private ToolStripMenuItem? _updateMenuItem;
    private ToolStripMenuItem? _restartMenuItem;
    private EventHandler? _updateClickHandler;

    public TrayApplicationContext()
    {
        _logProvider = new InMemoryLogProvider();

        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.AddProvider(_logProvider);
            builder.SetMinimumLevel(LogLevel.Information);
        });
        _logger = _loggerFactory.CreateLogger<TrayApplicationContext>();

        _requestHandler = new IpcRequestHandler(_logger);
        _ipcServer = new IpcServer(_requestHandler.HandleAsync, _logger);

        _updateChecker = new UpdateChecker(_loggerFactory.CreateLogger<UpdateChecker>());

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = $"HA PC Remote {VersionString}",
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };

        _ = Task.Run(() => RunIpcServerAsync(_cts.Token));

        // Check for updates after 30s, then every 4 hours
        _updateTimer = new System.Windows.Forms.Timer { Interval = 4 * 60 * 60 * 1000 };
        _updateTimer.Tick += async (_, _) => await SafeCheckForUpdateAsync();
        _updateTimer.Start();
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), _cts.Token);
            _notifyIcon.ContextMenuStrip!.BeginInvoke(async () => await SafeCheckForUpdateAsync());
        });

        _logger.LogInformation("HA PC Remote Tray {Version} started", VersionString);
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add($"HA PC Remote {VersionString}", null, null!).Enabled = false;
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Show Log", null, OnShowLog);
        menu.Items.Add("Show API Key", null, OnShowApiKey);

        _restartMenuItem = new ToolStripMenuItem("Restart Service");
        _restartMenuItem.Click += OnRestartService;
        menu.Items.Add(_restartMenuItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, OnExit);
        return menu;
    }

    private void OnShowLog(object? sender, EventArgs e)
    {
        _logViewerForm ??= new LogViewerForm(_logProvider);
        _logViewerForm.Show();
        _logViewerForm.BringToFront();
    }

    private void OnShowApiKey(object? sender, EventArgs e)
    {
        using var dialog = new ApiKeyDialog();
        dialog.ShowDialog();
    }

    private async void OnRestartService(object? sender, EventArgs e)
    {
        if (_restartMenuItem is null) return;

        _restartMenuItem.Enabled = false;
        _restartMenuItem.Text = "Restarting...";
        try
        {
            await Services.ServiceController.RestartAsync(_logger, _cts.Token);
        }
        finally
        {
            if (_restartMenuItem is not null)
            {
                _restartMenuItem.Text = "Restart Service";
                _restartMenuItem.Enabled = true;
            }
        }
    }

    private async Task SafeCheckForUpdateAsync()
    {
        try
        {
            await CheckForUpdateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update check failed unexpectedly");
        }
    }

    private async Task CheckForUpdateAsync()
    {
        var release = await _updateChecker.CheckAsync(_cts.Token);
        if (release is null) return;

        // Add or update the menu item
        if (_updateMenuItem is null)
        {
            _updateMenuItem = new ToolStripMenuItem
            {
                Name = "update",
                BackColor = Color.FromArgb(50, 80, 50)
            };
            _notifyIcon.ContextMenuStrip!.Items.Insert(2, _updateMenuItem);
        }

        _updateMenuItem.Text = $"Update to {release.TagName}";

        // Remove previous handler (stored reference so -= works correctly)
        if (_updateClickHandler is not null)
            _updateMenuItem.Click -= _updateClickHandler;

        _updateClickHandler = async (_, _) =>
        {
            _updateMenuItem!.Enabled = false;
            _updateMenuItem.Text = "Downloading...";

            if (await _updateChecker.DownloadAndInstallAsync(release, _cts.Token))
            {
                Application.Exit();
            }
            else
            {
                _updateMenuItem.Text = $"Update to {release.TagName}";
                _updateMenuItem.Enabled = true;
            }
        };
        _updateMenuItem.Click += _updateClickHandler;

        _notifyIcon.ShowBalloonTip(
            5000,
            "Update Available",
            $"HA PC Remote {release.TagName} is available. Right-click the tray icon to update.",
            ToolTipIcon.Info);
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

    private static string GetVersionString()
    {
        var version = UpdateChecker.GetCurrentVersion();
        return version is null ? "" : $"v{version.ToString(3)}";
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
            _updateTimer.Dispose();
            _logViewerForm?.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _loggerFactory.Dispose();
            _cts.Dispose();
        }
        base.Dispose(disposing);
    }
}
