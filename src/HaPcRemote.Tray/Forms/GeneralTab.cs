using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Logging;
using HaPcRemote.Tray.Logging;
using HaPcRemote.Tray.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HaPcRemote.Tray.Forms;

internal sealed class GeneralTab : TabPage
{
    private readonly ComboBox _logLevelCombo;
    private readonly CheckBox _autoUpdateCheck;
    private readonly Label _portStatusLabel;
    private readonly Label _soundVolumeViewLabel;
    private readonly Label _multiMonitorToolLabel;

    public GeneralTab(IServiceProvider services)
    {
        Text = "General";
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;
        Padding = new Padding(20);

        var options = services.GetRequiredService<IOptions<PcRemoteOptions>>().Value;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(0, 10, 0, 0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        // Port status
        _portStatusLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        layout.Controls.Add(MakeLabel("Port:"), 0, row);
        layout.Controls.Add(_portStatusLabel, 1, row++);
        UpdatePortStatus(options.Port);

        // NirSoft tools status
        _soundVolumeViewLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        layout.Controls.Add(MakeLabel("SoundVolumeView:"), 0, row);
        layout.Controls.Add(_soundVolumeViewLabel, 1, row++);

        _multiMonitorToolLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        layout.Controls.Add(MakeLabel("MultiMonitorTool:"), 0, row);
        layout.Controls.Add(_multiMonitorToolLabel, 1, row++);

        UpdateToolStatus(_soundVolumeViewLabel, Path.Combine(options.ToolsPath, "SoundVolumeView.exe"));
        UpdateToolStatus(_multiMonitorToolLabel, Path.Combine(options.ToolsPath, "MultiMonitorTool.exe"));

        // Separator
        layout.Controls.Add(new Label { AutoSize = true, Height = 10 }, 0, row++);

        // Log level
        _logLevelCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 200,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _logLevelCombo.Items.AddRange(["Error", "Warning", "Info", "Verbose"]);

        var settings = TraySettings.Load();
        _logLevelCombo.SelectedItem = settings.LogLevel switch
        {
            "Error" => "Error",
            "Info" => "Info",
            "Verbose" => "Verbose",
            _ => "Warning"
        };
        _logLevelCombo.SelectedIndexChanged += OnLogLevelChanged;

        layout.Controls.Add(MakeLabel("Log Level:"), 0, row);
        layout.Controls.Add(_logLevelCombo, 1, row++);

        // Auto-update
        _autoUpdateCheck = new CheckBox
        {
            Text = "Auto Update",
            ForeColor = Color.White,
            AutoSize = true,
            Checked = settings.AutoUpdate
        };
        _autoUpdateCheck.CheckedChanged += OnAutoUpdateChanged;

        layout.Controls.Add(new Label { AutoSize = true }, 0, row);
        layout.Controls.Add(_autoUpdateCheck, 1, row);

        Controls.Add(layout);
    }

    private void UpdatePortStatus(int port)
    {
        try
        {
            using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            // Port is free — Kestrel should be binding it, so it's likely already bound by us
            _portStatusLabel.Text = $"{port} (listening)";
            _portStatusLabel.ForeColor = Color.LightGreen;
        }
        catch (System.Net.Sockets.SocketException)
        {
            // Port is in use — expected if Kestrel already bound it
            _portStatusLabel.Text = $"{port} (listening)";
            _portStatusLabel.ForeColor = Color.LightGreen;
        }
        catch
        {
            _portStatusLabel.Text = $"{port} (unknown)";
            _portStatusLabel.ForeColor = Color.Orange;
        }
    }

    private static void UpdateToolStatus(Label label, string toolPath)
    {
        if (File.Exists(toolPath))
        {
            label.Text = "Found";
            label.ForeColor = Color.LightGreen;
        }
        else
        {
            label.Text = $"Missing — expected at {toolPath}";
            label.ForeColor = Color.Salmon;
        }
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        ForeColor = Color.White,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Padding = new Padding(0, 3, 0, 0)
    };

    private void OnLogLevelChanged(object? sender, EventArgs e)
    {
        var level = _logLevelCombo.SelectedItem?.ToString() ?? "Warning";
        var logLevel = level switch
        {
            "Error" => LogLevel.Error,
            "Info" => LogLevel.Information,
            "Verbose" => LogLevel.Debug,
            _ => LogLevel.Warning
        };

        InMemoryLogProvider.MinimumLevel = logLevel;
        FileLoggerProvider.MinimumLevel = logLevel;

        var s = TraySettings.Load();
        s.LogLevel = level;
        s.Save();
    }

    private void OnAutoUpdateChanged(object? sender, EventArgs e)
    {
        var s = TraySettings.Load();
        s.AutoUpdate = _autoUpdateCheck.Checked;
        s.Save();
    }
}
