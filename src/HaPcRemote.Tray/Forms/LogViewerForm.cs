using HaPcRemote.Tray.Logging;

using Microsoft.Extensions.Logging;

namespace HaPcRemote.Tray.Forms;

internal sealed class LogViewerForm : Form
{
    private readonly InMemoryLogProvider _provider;
    private readonly RichTextBox _logBox;

    public LogViewerForm(InMemoryLogProvider provider)
    {
        _provider = provider;

        Text = "HA PC Remote - Log";
        Size = new Size(800, 500);
        MinimumSize = new Size(400, 300);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application;

        _logBox = new RichTextBox
        {
            ReadOnly = true,
            Multiline = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White,
            Font = new Font("Consolas", 9.5f),
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            WordWrap = false
        };

        var clearButton = new Button
        {
            Text = "Clear",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            Size = new Size(75, 28),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        clearButton.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        clearButton.Click += (_, _) => _logBox.Clear();

        var bottomPanel = new Panel
        {
            Height = 40,
            Dock = DockStyle.Bottom,
            BackColor = Color.FromArgb(30, 30, 30),
            Padding = new Padding(0, 6, 8, 6)
        };
        clearButton.Dock = DockStyle.Right;
        bottomPanel.Controls.Add(clearButton);

        Controls.Add(_logBox);
        Controls.Add(bottomPanel);

        _provider.OnLogEntry += OnNewLogEntry;
    }

    private void OnNewLogEntry(LogEntry entry)
    {
        if (IsDisposed) return;

        try
        {
            BeginInvoke(() => AppendEntry(entry));
        }
        catch (ObjectDisposedException)
        {
            // Form disposed between check and invoke — ignore
        }
    }

    private void AppendEntry(LogEntry entry)
    {
        var color = entry.Level switch
        {
            LogLevel.Error => Color.FromArgb(255, 100, 100),
            LogLevel.Critical => Color.FromArgb(255, 100, 100),
            LogLevel.Warning => Color.FromArgb(255, 200, 100),
            _ => Color.White
        };

        var level = entry.Level switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???"
        };

        var line = $"[{entry.Timestamp:HH:mm:ss}] [{level}] {entry.Category} - {entry.Message}\n";

        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionLength = 0;
        _logBox.SelectionColor = color;
        _logBox.AppendText(line);
        _logBox.ScrollToCaret();
    }

    private void LoadExistingEntries()
    {
        _logBox.Clear();
        foreach (var entry in _provider.GetEntries())
            AppendEntry(entry);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) LoadExistingEntries();
    }
}
