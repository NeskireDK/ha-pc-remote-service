using System.Text.Json.Nodes;
using HaPcRemote.Shared.Configuration;

namespace HaPcRemote.Tray.Forms;

internal sealed class ApiKeyDialog : Form
{
    private readonly Button _copyButton;
    private readonly TextBox _keyTextBox;

    public ApiKeyDialog()
    {
        Text = "API Key";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 160);
        BackColor = Color.FromArgb(45, 45, 48);
        ForeColor = Color.White;
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application;

        var label = new Label
        {
            Text = "API Key:",
            Location = new Point(16, 16),
            AutoSize = true
        };

        var apiKey = ReadApiKey();

        _keyTextBox = new TextBox
        {
            Text = string.IsNullOrEmpty(apiKey) ? "No API key configured" : apiKey,
            ReadOnly = true,
            Font = new Font("Consolas", 10f),
            Location = new Point(16, 42),
            Size = new Size(388, 28),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White
        };
        _keyTextBox.Click += (_, _) => _keyTextBox.SelectAll();

        _copyButton = new Button
        {
            Text = "Copy",
            Location = new Point(216, 110),
            Size = new Size(90, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 65),
            ForeColor = Color.White,
            Enabled = !string.IsNullOrEmpty(apiKey)
        };
        _copyButton.Click += OnCopyClick;

        var closeButton = new Button
        {
            Text = "Close",
            Location = new Point(314, 110),
            Size = new Size(90, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 65),
            ForeColor = Color.White,
            DialogResult = DialogResult.Cancel
        };

        CancelButton = closeButton;
        Controls.AddRange([label, _keyTextBox, _copyButton, closeButton]);
    }

    private async void OnCopyClick(object? sender, EventArgs e)
    {
        Clipboard.SetText(_keyTextBox.Text);
        _copyButton.Text = "Copied!";
        _copyButton.Enabled = false;

        await Task.Delay(1500);

        if (!IsDisposed)
        {
            _copyButton.Text = "Copy";
            _copyButton.Enabled = true;
        }
    }

    private static string ReadApiKey()
    {
        try
        {
            var path = ConfigPaths.GetWritableConfigPath();
            if (!File.Exists(path)) return "";
            var json = JsonNode.Parse(File.ReadAllText(path));
            return json?["PcRemote"]?["Auth"]?["ApiKey"]?.GetValue<string>() ?? "";
        }
        catch
        {
            return "";
        }
    }
}
