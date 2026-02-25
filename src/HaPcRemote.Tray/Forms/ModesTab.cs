using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Services;
using Microsoft.Extensions.Options;

namespace HaPcRemote.Tray.Forms;

internal sealed class ModesTab : TabPage
{
    private readonly IConfigurationWriter _configWriter;
    private readonly IAudioService _audioService;
    private readonly IMonitorService _monitorService;
    private readonly IOptions<PcRemoteOptions> _options;

    private readonly ListBox _modeList;
    private readonly TextBox _modeNameBox;
    private readonly ComboBox _audioDeviceCombo;
    private readonly ComboBox _monitorProfileCombo;
    private readonly TrackBar _volumeSlider;
    private readonly Label _volumeLabel;
    private readonly ComboBox _launchAppCombo;
    private readonly ComboBox _killAppCombo;
    private readonly Button _saveButton;
    private readonly Button _deleteButton;
    private readonly Button _newButton;

    public ModesTab(IServiceProvider services)
    {
        Text = "PC Modes";
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;
        Padding = new Padding(10);

        _configWriter = services.GetRequiredService<IConfigurationWriter>();
        _audioService = services.GetRequiredService<IAudioService>();
        _monitorService = services.GetRequiredService<IMonitorService>();
        _options = services.GetRequiredService<IOptions<PcRemoteOptions>>();

        // Left panel: mode list + buttons
        var leftPanel = new Panel { Dock = DockStyle.Left, Width = 180, Padding = new Padding(0, 0, 10, 0) };

        _modeList = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(45, 45, 45),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9.5f)
        };
        _modeList.SelectedIndexChanged += OnModeSelected;

        var listButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 35,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = false
        };
        _newButton = CreateButton("New");
        _newButton.Click += OnNewMode;
        _deleteButton = CreateButton("Delete");
        _deleteButton.Click += OnDeleteMode;
        listButtons.Controls.Add(_newButton);
        listButtons.Controls.Add(_deleteButton);

        leftPanel.Controls.Add(_modeList);
        leftPanel.Controls.Add(listButtons);

        // Right panel: mode editor
        var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 0, 0, 0) };
        var editLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(0)
        };
        editLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        editLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        // Mode name
        _modeNameBox = new TextBox { BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, Width = 250, BorderStyle = BorderStyle.FixedSingle };
        editLayout.Controls.Add(MakeLabel("Name:"), 0, row);
        editLayout.Controls.Add(_modeNameBox, 1, row++);

        // Audio device (with "Don't change" option)
        _audioDeviceCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 250,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        editLayout.Controls.Add(MakeLabel("Audio Device:"), 0, row);
        editLayout.Controls.Add(_audioDeviceCombo, 1, row++);

        // Monitor profile
        _monitorProfileCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 250,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        editLayout.Controls.Add(MakeLabel("Monitor Profile:"), 0, row);
        editLayout.Controls.Add(_monitorProfileCombo, 1, row++);

        // Volume
        var volumePanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        _volumeSlider = new TrackBar { Minimum = 0, Maximum = 100, Width = 200, TickFrequency = 10, Value = 50 };
        _volumeLabel = new Label { Text = "50", ForeColor = Color.White, AutoSize = true, Padding = new Padding(5, 5, 0, 0) };
        _volumeSlider.ValueChanged += (_, _) => _volumeLabel.Text = _volumeSlider.Value.ToString();
        volumePanel.Controls.Add(_volumeSlider);
        volumePanel.Controls.Add(_volumeLabel);
        editLayout.Controls.Add(MakeLabel("Volume:"), 0, row);
        editLayout.Controls.Add(volumePanel, 1, row++);

        // Launch app
        _launchAppCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 250,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        editLayout.Controls.Add(MakeLabel("Launch App:"), 0, row);
        editLayout.Controls.Add(_launchAppCombo, 1, row++);

        // Kill app
        _killAppCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 250,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        editLayout.Controls.Add(MakeLabel("Kill App:"), 0, row);
        editLayout.Controls.Add(_killAppCombo, 1, row++);

        // Save button
        _saveButton = CreateButton("Save Mode");
        _saveButton.Width = 120;
        _saveButton.Click += OnSaveMode;
        editLayout.Controls.Add(new Label(), 0, row);
        editLayout.Controls.Add(_saveButton, 1, row);

        rightPanel.Controls.Add(editLayout);

        Controls.Add(rightPanel);
        Controls.Add(leftPanel);

        LoadModes();
    }

    protected override async void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) await RefreshDropdownsAsync();
    }

    private async Task RefreshDropdownsAsync()
    {
        try
        {
            _audioDeviceCombo.Items.Clear();
            _audioDeviceCombo.Items.Add("(Don't change)");
            var devices = await _audioService.GetDevicesAsync();
            foreach (var d in devices)
                _audioDeviceCombo.Items.Add(d.Name);

            _monitorProfileCombo.Items.Clear();
            _monitorProfileCombo.Items.Add("(Don't change)");
            var profiles = await _monitorService.GetProfilesAsync();
            foreach (var p in profiles)
                _monitorProfileCombo.Items.Add(p.Name);
        }
        catch
        {
            // Services may fail if tools aren't installed
        }

        RefreshAppDropdowns();
    }

    private void RefreshAppDropdowns()
    {
        var apps = _options.Value.Apps;

        _launchAppCombo.Items.Clear();
        _launchAppCombo.Items.Add(new AppDropdownItem(null, "(None)"));
        _killAppCombo.Items.Clear();
        _killAppCombo.Items.Add(new AppDropdownItem(null, "(None)"));

        foreach (var (key, app) in apps)
        {
            var item = new AppDropdownItem(key, app.DisplayName);
            _launchAppCombo.Items.Add(item);
            _killAppCombo.Items.Add(item);
        }
    }

    private static void SelectAppItem(ComboBox combo, string? appKey)
    {
        if (string.IsNullOrEmpty(appKey))
        {
            combo.SelectedIndex = 0;
            return;
        }

        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is AppDropdownItem item && item.Key == appKey)
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        combo.SelectedIndex = 0;
    }

    private static string? GetSelectedAppKey(ComboBox combo)
        => (combo.SelectedItem as AppDropdownItem)?.Key;

    private void LoadModes()
    {
        _modeList.Items.Clear();
        var options = _configWriter.Read();
        foreach (var name in options.Modes.Keys)
            _modeList.Items.Add(name);
    }

    private void OnModeSelected(object? sender, EventArgs e)
    {
        if (_modeList.SelectedItem is not string name) return;
        var options = _configWriter.Read();
        if (!options.Modes.TryGetValue(name, out var mode)) return;

        _modeNameBox.Text = name;
        _audioDeviceCombo.SelectedItem = mode.AudioDevice ?? "(Don't change)";
        _monitorProfileCombo.SelectedItem = mode.MonitorProfile ?? "(Don't change)";
        _volumeSlider.Value = mode.Volume ?? 50;
        SelectAppItem(_launchAppCombo, mode.LaunchApp);
        SelectAppItem(_killAppCombo, mode.KillApp);
    }

    private void OnNewMode(object? sender, EventArgs e)
    {
        _modeList.ClearSelected();
        _modeNameBox.Text = "";
        if (_audioDeviceCombo.Items.Count > 0)
            _audioDeviceCombo.SelectedIndex = 0;
        if (_monitorProfileCombo.Items.Count > 0)
            _monitorProfileCombo.SelectedIndex = 0;
        _volumeSlider.Value = 50;
        if (_launchAppCombo.Items.Count > 0) _launchAppCombo.SelectedIndex = 0;
        if (_killAppCombo.Items.Count > 0) _killAppCombo.SelectedIndex = 0;
        _modeNameBox.Focus();
    }

    private void OnSaveMode(object? sender, EventArgs e)
    {
        var name = _modeNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Mode name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var mode = new ModeConfig
        {
            AudioDevice = _audioDeviceCombo.SelectedItem?.ToString() is "(Don't change)" ? null : _audioDeviceCombo.SelectedItem?.ToString(),
            MonitorProfile = _monitorProfileCombo.SelectedItem?.ToString() is "(Don't change)" ? null : _monitorProfileCombo.SelectedItem?.ToString(),
            Volume = _volumeSlider.Value,
            LaunchApp = GetSelectedAppKey(_launchAppCombo),
            KillApp = GetSelectedAppKey(_killAppCombo)
        };

        // If renaming (selected name differs from text box), delete old
        if (_modeList.SelectedItem is string oldName && oldName != name)
            _configWriter.DeleteMode(oldName);

        _configWriter.SaveMode(name, mode);
        LoadModes();

        // Re-select the saved mode
        var idx = _modeList.Items.IndexOf(name);
        if (idx >= 0) _modeList.SelectedIndex = idx;
    }

    private void OnDeleteMode(object? sender, EventArgs e)
    {
        if (_modeList.SelectedItem is not string name) return;
        if (MessageBox.Show($"Delete mode '{name}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _configWriter.DeleteMode(name);
        LoadModes();
        OnNewMode(null, EventArgs.Empty);
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        ForeColor = Color.White,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Padding = new Padding(0, 6, 0, 0)
    };

    private static Button CreateButton(string text) => new()
    {
        Text = text,
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(50, 50, 50),
        ForeColor = Color.White,
        Size = new Size(75, 28),
        Cursor = Cursors.Hand
    };

    private sealed class AppDropdownItem(string? key, string displayName)
    {
        public string? Key { get; } = key;
        public override string ToString() => displayName;
    }
}
