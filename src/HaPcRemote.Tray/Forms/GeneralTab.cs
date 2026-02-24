using HaPcRemote.Service.Logging;
using HaPcRemote.Tray.Logging;
using HaPcRemote.Tray.Models;
using Microsoft.Extensions.Logging;

namespace HaPcRemote.Tray.Forms;

internal sealed class GeneralTab : TabPage
{
    private readonly ComboBox _logLevelCombo;
    private readonly CheckBox _autoUpdateCheck;

    public GeneralTab()
    {
        Text = "General";
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;
        Padding = new Padding(20);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(0, 10, 0, 0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Log level
        var logLabel = new Label { Text = "Log Level:", ForeColor = Color.White, AutoSize = true, Anchor = AnchorStyles.Left };
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

        layout.Controls.Add(logLabel, 0, 0);
        layout.Controls.Add(_logLevelCombo, 1, 0);

        // Auto-update
        _autoUpdateCheck = new CheckBox
        {
            Text = "Auto Update",
            ForeColor = Color.White,
            AutoSize = true,
            Checked = settings.AutoUpdate
        };
        _autoUpdateCheck.CheckedChanged += OnAutoUpdateChanged;

        layout.Controls.Add(new Label { AutoSize = true }, 0, 1);
        layout.Controls.Add(_autoUpdateCheck, 1, 1);

        Controls.Add(layout);
    }

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
