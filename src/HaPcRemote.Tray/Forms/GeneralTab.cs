using System.Diagnostics;
using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Logging;
using HaPcRemote.Service.Services;
using HaPcRemote.Tray.Logging;
using HaPcRemote.Tray.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HaPcRemote.Tray.Forms;

internal sealed class GeneralTab : TabPage
{
    private readonly IConfigurationWriter _configWriter;
    private readonly ComboBox _logLevelCombo;
    private readonly CheckBox _autoUpdateCheck;
    private readonly NumericUpDown _portInput;
    private readonly Label _portStatusLabel;
    private readonly Button _portSaveButton;
    private readonly Label _soundVolumeViewLabel;
    private readonly Label _multiMonitorToolLabel;
    private readonly int _currentPort;

    public GeneralTab(IServiceProvider services)
    {
        Text = "General";
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;
        Padding = new Padding(20);

        _configWriter = services.GetRequiredService<IConfigurationWriter>();
        var options = services.GetRequiredService<IOptions<PcRemoteOptions>>().Value;
        _currentPort = options.Port;

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

        // Port input + status
        var portPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        _portInput = new NumericUpDown
        {
            Minimum = 1024,
            Maximum = 65535,
            Value = _currentPort,
            Width = 80,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White
        };
        _portStatusLabel = new Label { AutoSize = true, Padding = new Padding(5, 3, 0, 0) };
        _portSaveButton = new Button
        {
            Text = "Save & Restart",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            AutoSize = true,
            Visible = false,
            Cursor = Cursors.Hand
        };
        _portSaveButton.Click += OnPortSave;
        _portInput.ValueChanged += (_, _) =>
        {
            _portSaveButton.Visible = (int)_portInput.Value != _currentPort;
        };
        portPanel.Controls.Add(_portInput);
        portPanel.Controls.Add(_portStatusLabel);
        portPanel.Controls.Add(_portSaveButton);
        layout.Controls.Add(MakeLabel("Port:"), 0, row);
        layout.Controls.Add(portPanel, 1, row++);
        UpdatePortStatus();

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

    private void UpdatePortStatus()
    {
        // Deferred check — Kestrel starts async after Build()
        _portStatusLabel.Text = "starting...";
        _portStatusLabel.ForeColor = Color.Orange;

        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            BeginInvoke(() =>
            {
                if (KestrelStatus.IsRunning)
                {
                    _portStatusLabel.Text = "listening";
                    _portStatusLabel.ForeColor = Color.LightGreen;
                }
                else if (KestrelStatus.Error is not null)
                {
                    _portStatusLabel.Text = $"failed: {KestrelStatus.Error}";
                    _portStatusLabel.ForeColor = Color.Salmon;
                    _portSaveButton.Visible = true;
                }
                else
                {
                    _portStatusLabel.Text = "starting...";
                    _portStatusLabel.ForeColor = Color.Orange;
                }
            });
        });
    }

    private void OnPortSave(object? sender, EventArgs e)
    {
        var newPort = (int)_portInput.Value;
        if (MessageBox.Show(
                $"Change port to {newPort} and restart the application?",
                "Confirm Restart",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _configWriter.SavePort(newPort);
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true });
        Application.Exit();
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
