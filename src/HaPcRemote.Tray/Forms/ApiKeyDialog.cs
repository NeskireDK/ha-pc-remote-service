using System.Text.Json.Nodes;
using HaPcRemote.Shared.Configuration;

namespace HaPcRemote.Tray.Forms;

internal sealed class ApiKeyDialog : Form
{
    public ApiKeyDialog()
    {
        Text = "API Key";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 130);
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

        var keyTextBox = new TextBox
        {
            Text = string.IsNullOrEmpty(apiKey) ? "No API key configured" : apiKey,
            ReadOnly = true,
            Font = new Font("Consolas", 10f),
            Location = new Point(16, 42),
            Size = new Size(388, 28),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White
        };
        keyTextBox.Click += (_, _) => keyTextBox.SelectAll();

        var closeButton = new Button
        {
            Text = "Close",
            Location = new Point(314, 84),
            Size = new Size(90, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 65),
            ForeColor = Color.White,
            DialogResult = DialogResult.Cancel
        };

        CancelButton = closeButton;
        Controls.AddRange([label, keyTextBox, closeButton]);
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
