using HaPcRemote.Service.Configuration;
using HaPcRemote.Service.Services;

namespace HaPcRemote.Tray.Forms;

internal sealed class PowerTab : TabPage
{
    private readonly IConfigurationWriter _configWriter;
    private readonly CheckBox _sleepOnDisconnect;
    private readonly NumericUpDown _sleepDelay;

    public PowerTab(IServiceProvider services)
    {
        Text = "Power";
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;
        Padding = new Padding(20);

        _configWriter = services.GetRequiredService<IConfigurationWriter>();
        var current = _configWriter.Read().Power;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(0, 10, 0, 0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Sleep on disconnect
        _sleepOnDisconnect = new CheckBox
        {
            Text = "Sleep when HA disconnects",
            ForeColor = Color.White,
            AutoSize = true,
            Checked = current.SleepOnDisconnect
        };
        layout.Controls.Add(_sleepOnDisconnect, 0, 0);
        layout.SetColumnSpan(_sleepOnDisconnect, 2);

        // Delay
        var delayLabel = new Label
        {
            Text = "Delay before sleep (min):",
            ForeColor = Color.White,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(0, 8, 0, 0)
        };
        _sleepDelay = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 60,
            Value = Math.Clamp(current.SleepDelayMinutes, 1, 60),
            Width = 80,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White
        };
        layout.Controls.Add(delayLabel, 0, 1);
        layout.Controls.Add(_sleepDelay, 1, 1);

        // Save button
        var saveButton = new Button
        {
            Text = "Save",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            Size = new Size(100, 30),
            Cursor = Cursors.Hand
        };
        saveButton.Click += OnSave;
        layout.Controls.Add(new Label(), 0, 2);
        layout.Controls.Add(saveButton, 1, 2);

        Controls.Add(layout);
    }

    private void OnSave(object? sender, EventArgs e)
    {
        _configWriter.SavePowerSettings(new PowerSettings
        {
            SleepOnDisconnect = _sleepOnDisconnect.Checked,
            SleepDelayMinutes = (int)_sleepDelay.Value
        });
    }
}
