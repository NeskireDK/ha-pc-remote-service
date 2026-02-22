using HaPcRemote.Tray.Logging;
using Microsoft.Extensions.Logging;

namespace HaPcRemote.Tray.Services;

/// <summary>
/// Tails the service log file and feeds parsed entries into InMemoryLogProvider.
/// Format: timestamp|level|category|message
/// </summary>
internal sealed class ServiceLogTailer : IDisposable
{
    private readonly string _logFilePath;
    private readonly InMemoryLogProvider _logProvider;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private long _lastPosition;

    public ServiceLogTailer(string logFilePath, InMemoryLogProvider logProvider, ILogger logger)
    {
        _logFilePath = logFilePath;
        _logProvider = logProvider;
        _logger = logger;
    }

    public void Start()
    {
        // Skip existing content on startup — only show new entries
        if (File.Exists(_logFilePath))
            _lastPosition = new FileInfo(_logFilePath).Length;

        _ = Task.Run(() => TailLoopAsync(_cts.Token));
    }

    private async Task TailLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                ReadNewLines();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error reading service log file");
            }

            try
            {
                await Task.Delay(1000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void ReadNewLines()
    {
        if (!File.Exists(_logFilePath))
        {
            _lastPosition = 0;
            return;
        }

        var info = new FileInfo(_logFilePath);

        // File was rotated (smaller than last position)
        if (info.Length < _lastPosition)
            _lastPosition = 0;

        if (info.Length == _lastPosition) return;

        using var stream = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(_lastPosition, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);

        while (reader.ReadLine() is { } line)
        {
            if (TryParseLine(line, out var entry))
                _logProvider.AddEntry(entry);
        }

        _lastPosition = stream.Position;
    }

    private static bool TryParseLine(string line, out LogEntry entry)
    {
        entry = default!;
        if (string.IsNullOrWhiteSpace(line)) return false;

        // Format: timestamp|level|category|message
        var parts = line.Split('|', 4);
        if (parts.Length < 4) return false;

        if (!DateTime.TryParse(parts[0], out var timestamp))
            return false;

        var level = parts[1] switch
        {
            "TRC" => LogLevel.Trace,
            "DBG" => LogLevel.Debug,
            "INF" => LogLevel.Information,
            "WRN" => LogLevel.Warning,
            "ERR" => LogLevel.Error,
            "CRT" => LogLevel.Critical,
            _ => LogLevel.None
        };

        if (level == LogLevel.None) return false;

        var message = parts[3].Replace("\\n", "\n");
        entry = new LogEntry(timestamp, level, parts[2], message);
        return true;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
