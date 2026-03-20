using System.Diagnostics;
using HaPcRemote.Tray.Logging;
using Microsoft.Extensions.Logging;

namespace HaPcRemote.Tray.Forms;

internal sealed class LogTab : TabPage, ISettingsTab
{
    private readonly InMemoryLogProvider _provider;
    private readonly RichTextBox _logBox;
    private readonly int _port;

    public LogTab(InMemoryLogProvider provider, int port)
    {
        _provider = provider;

        Text = "Log";
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;

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
            WordWrap = true,
            ShortcutsEnabled = true
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.BackColor = Color.FromArgb(45, 45, 45);
        contextMenu.ForeColor = Color.White;
        contextMenu.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());
        var copyItem = new ToolStripMenuItem("Copy");
        copyItem.Click += (_, _) => _logBox.Copy();
        contextMenu.Items.Add(copyItem);
        _logBox.ContextMenuStrip = contextMenu;

        _port = port;

        Controls.Add(_logBox);

        _provider.OnLogEntry += OnNewLogEntry;
    }

    public IEnumerable<Button> CreateFooterButtons()
    {
        var clearButton = TabFooter.MakeButton("Clear");
        clearButton.Click += (_, _) => _logBox.Clear();

        var debugButton = TabFooter.MakeButton("API Explorer");
        debugButton.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo($"http://localhost:{_port}/api-explorer") { UseShellExecute = true }); }
            catch { /* best effort */ }
        };

        return [debugButton, clearButton];
    }

    private void OnNewLogEntry(LogEntry entry)
    {
        if (IsDisposed || !IsHandleCreated) return;

        try
        {
            BeginInvoke(() => AppendEntry(entry));
        }
        catch (ObjectDisposedException)
        {
            // Form disposed between check and invoke
        }
    }

    private bool IsScrolledToBottom()
    {
        // Compare visible bottom position to total content height
        var scrollInfo = new NativeMethods.ScrollInfo
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.ScrollInfo>(),
            fMask = NativeMethods.SIF_ALL
        };
        NativeMethods.GetScrollInfo(_logBox.Handle, NativeMethods.SB_VERT, ref scrollInfo);
        return scrollInfo.nPos >= scrollInfo.nMax - scrollInfo.nPage;
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

        var pinToBottom = IsScrolledToBottom();

        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionLength = 0;
        _logBox.SelectionColor = color;
        _logBox.AppendText(line);

        if (pinToBottom)
            _logBox.ScrollToCaret();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) LoadExistingEntries();
    }

    private void LoadExistingEntries()
    {
        _logBox.SuspendLayout();
        _logBox.Clear();
        foreach (var entry in _provider.GetEntries())
            AppendEntry(entry);
        _logBox.ResumeLayout();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _provider.OnLogEntry -= OnNewLogEntry;
        base.Dispose(disposing);
    }
}

file static class NativeMethods
{
    internal const int SB_VERT = 1;
    internal const uint SIF_ALL = 0x17;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct ScrollInfo
    {
        public int cbSize;
        public uint fMask;
        public int nMin;
        public int nMax;
        public uint nPage;
        public int nPos;
        public int nTrackPos;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    internal static extern bool GetScrollInfo(IntPtr hwnd, int fnBar, ref ScrollInfo lpsi);
}

file sealed class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuItemSelected => Color.FromArgb(60, 60, 60);
    public override Color MenuItemBorder => Color.FromArgb(80, 80, 80);
    public override Color MenuBorder => Color.FromArgb(80, 80, 80);
    public override Color ToolStripDropDownBackground => Color.FromArgb(45, 45, 45);
    public override Color ImageMarginGradientBegin => Color.FromArgb(45, 45, 45);
    public override Color ImageMarginGradientMiddle => Color.FromArgb(45, 45, 45);
    public override Color ImageMarginGradientEnd => Color.FromArgb(45, 45, 45);
}
