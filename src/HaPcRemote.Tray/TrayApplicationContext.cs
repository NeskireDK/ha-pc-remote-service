using HaPcRemote.Tray.Forms;
using HaPcRemote.Tray.Logging;
using HaPcRemote.Tray.Models;
using HaPcRemote.Tray.Services;
using Microsoft.Extensions.Logging;

namespace HaPcRemote.Tray;

/// <summary>
/// WinForms application context. Hosts the system tray icon and log viewer.
/// Kestrel runs alongside via TrayWebHost.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly string VersionString = GetVersionString();

    private readonly NotifyIcon _notifyIcon;
    private readonly CancellationTokenSource _cts = new();
    private readonly CancellationTokenSource _webCts;
    private readonly ILogger _logger;
    private readonly InMemoryLogProvider _logProvider;
    private readonly UpdateChecker _updateChecker;
    private readonly System.Windows.Forms.Timer _updateTimer;

    private LogViewerForm? _logViewerForm;
    private ToolStripMenuItem? _updateMenuItem;
    private ToolStripMenuItem? _autoUpdateMenuItem;
    private ToolStripMenuItem? _debugLoggingMenuItem;
    private UpdateChecker.ReleaseInfo? _pendingRelease;

    public TrayApplicationContext(IServiceProvider webServices, CancellationTokenSource webCts, InMemoryLogProvider logProvider)
    {
        _webCts = webCts;
        _logProvider = logProvider;

        var loggerFactory = webServices.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger<TrayApplicationContext>();

        _updateChecker = new UpdateChecker(loggerFactory.CreateLogger<UpdateChecker>());

        var settings = TraySettings.Load();
        InMemoryLogProvider.DebugEnabled = settings.DebugLogging;

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = $"HA PC Remote {VersionString}",
            Visible = true,
            ContextMenuStrip = BuildContextMenu(settings)
        };

        // Check for updates after 30s, then on timer
        _updateTimer = new System.Windows.Forms.Timer { Interval = GetUpdateTimerInterval(settings.AutoUpdate) };
        _updateTimer.Tick += async (_, _) => await SafeCheckForUpdateAsync(showProgress: false);
        _updateTimer.Start();
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), _cts.Token);
            _notifyIcon.ContextMenuStrip!.BeginInvoke(async () => await SafeCheckForUpdateAsync());
        });

        _logger.LogInformation("HA PC Remote Tray {Version} started", VersionString);
    }

    private ContextMenuStrip BuildContextMenu(TraySettings settings)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add($"HA PC Remote {VersionString}", null, null!).Enabled = false;
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Show Log", null, OnShowLog);
        menu.Items.Add("Show API Key", null, OnShowApiKey);

        menu.Items.Add(new ToolStripSeparator());

        _updateMenuItem = new ToolStripMenuItem("Check for updates");
        _updateMenuItem.Click += OnCheckForUpdatesClick;
        menu.Items.Add(_updateMenuItem);

        _autoUpdateMenuItem = new ToolStripMenuItem("Auto Update") { CheckOnClick = true, Checked = settings.AutoUpdate };
        _autoUpdateMenuItem.CheckedChanged += OnAutoUpdateToggled;
        menu.Items.Add(_autoUpdateMenuItem);

        _debugLoggingMenuItem = new ToolStripMenuItem("Debug Logging") { CheckOnClick = true, Checked = settings.DebugLogging };
        _debugLoggingMenuItem.CheckedChanged += OnDebugLoggingToggled;
        menu.Items.Add(_debugLoggingMenuItem);

        menu.Items.Add("Exit", null, OnExit);
        return menu;
    }

    private void OnShowLog(object? sender, EventArgs e)
    {
        _logViewerForm ??= new LogViewerForm(_logProvider);
        _logViewerForm.Show();
        _logViewerForm.BringToFront();
        _logViewerForm.Activate();
    }

    private void OnShowApiKey(object? sender, EventArgs e)
    {
        using var dialog = new ApiKeyDialog();
        dialog.ShowDialog();
    }

    private void OnAutoUpdateToggled(object? sender, EventArgs e)
    {
        var s = TraySettings.Load();
        s.AutoUpdate = _autoUpdateMenuItem!.Checked;
        s.Save();
        _updateTimer.Interval = GetUpdateTimerInterval(s.AutoUpdate);
        _logger.LogInformation("Auto update {State}", s.AutoUpdate ? "enabled" : "disabled");
    }

    private void OnDebugLoggingToggled(object? sender, EventArgs e)
    {
        var s = TraySettings.Load();
        s.DebugLogging = _debugLoggingMenuItem!.Checked;
        s.Save();
        InMemoryLogProvider.DebugEnabled = s.DebugLogging;
        _logger.LogInformation("Debug logging {State}", s.DebugLogging ? "enabled" : "disabled");
    }

    private static int GetUpdateTimerInterval(bool autoUpdate)
        => autoUpdate ? 5 * 60 * 1000 : 4 * 60 * 60 * 1000;

    private async void OnCheckForUpdatesClick(object? sender, EventArgs e)
    {
        if (_pendingRelease is not null)
            await HandleDownloadAsync(_pendingRelease);
        else
            await SafeCheckForUpdateAsync(showProgress: true);
    }

    private async Task SafeCheckForUpdateAsync(bool showProgress = false)
    {
        try
        {
            await CheckForUpdateAsync(showProgress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update check failed unexpectedly");
            if (showProgress && _updateMenuItem is not null)
            {
                _updateMenuItem.Text = "Check for updates";
                _updateMenuItem.Enabled = true;
            }
        }
    }

    private async Task CheckForUpdateAsync(bool showProgress = false)
    {
        if (_updateMenuItem is null) return;

        if (showProgress)
        {
            _updateMenuItem.Enabled = false;
            _updateMenuItem.BackColor = default;
            _updateMenuItem.Text = "Checking...";
        }

        var release = await _updateChecker.CheckAsync(_cts.Token);

        if (release is null)
        {
            if (showProgress)
            {
                _updateMenuItem.Text = "Up to date";
                var resetTimer = new System.Windows.Forms.Timer { Interval = 3000 };
                resetTimer.Tick += (_, _) =>
                {
                    resetTimer.Stop();
                    resetTimer.Dispose();
                    if (_updateMenuItem is not null && _pendingRelease is null)
                    {
                        _updateMenuItem.Text = "Check for updates";
                        _updateMenuItem.Enabled = true;
                    }
                };
                resetTimer.Start();
            }
            return;
        }

        _pendingRelease = release;
        _updateMenuItem.Text = $"Update to {release.TagName}";
        _updateMenuItem.BackColor = Color.FromArgb(50, 80, 50);
        _updateMenuItem.Enabled = true;

        _notifyIcon.ShowBalloonTip(
            5000,
            "Update Available",
            $"HA PC Remote {release.TagName} is available. Right-click the tray icon to update.",
            ToolTipIcon.Info);

        if (_autoUpdateMenuItem?.Checked == true)
        {
            var installTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            installTimer.Tick += async (_, _) =>
            {
                installTimer.Stop();
                installTimer.Dispose();
                if (_pendingRelease is not null)
                    await HandleDownloadAsync(_pendingRelease);
            };
            installTimer.Start();
        }
    }

    private async Task HandleDownloadAsync(UpdateChecker.ReleaseInfo release)
    {
        if (_updateMenuItem is null) return;

        _updateMenuItem.Enabled = false;
        _updateMenuItem.Text = "Updating…";

        if (await _updateChecker.DownloadAndInstallAsync(release, _cts.Token))
        {
            Application.Exit();
        }
        else
        {
            _updateMenuItem.Text = $"Update to {release.TagName}";
            _updateMenuItem.Enabled = true;
        }
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _logger.LogInformation("Shutting down...");
        _cts.Cancel();
        _webCts.Cancel();
        _notifyIcon.Visible = false;
        Application.Exit();
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
            _cts.Dispose();
            _updateTimer.Dispose();
            _logViewerForm?.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
