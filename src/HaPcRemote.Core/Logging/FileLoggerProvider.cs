using Microsoft.Extensions.Logging;

namespace HaPcRemote.Service.Logging;

/// <summary>
/// Writes log entries to a file in a structured, parseable format.
/// Format: timestamp|level|category|message (newlines in messages replaced with \n)
/// </summary>
internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly long _maxFileSize;
    private readonly Lock _lock = new();

    public FileLoggerProvider(string filePath, long maxFileSize = 5 * 1024 * 1024)
    {
        _filePath = filePath;
        _maxFileSize = maxFileSize;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        }
        catch
        {
            // Non-writable path (e.g. Linux/CI) — file logging will silently no-op
        }
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    internal void WriteEntry(DateTime timestamp, LogLevel level, string category, string message)
    {
        var escapedMessage = message.Replace("\r\n", "\\n").Replace("\n", "\\n");
        var line = $"{timestamp:O}|{LevelToString(level)}|{category}|{escapedMessage}";

        lock (_lock)
        {
            try
            {
                RotateIfNeeded();
                File.AppendAllText(_filePath, line + Environment.NewLine);
            }
            catch
            {
                // Don't let logging failures crash the service
            }
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(_filePath)) return;
        var info = new FileInfo(_filePath);
        if (info.Length < _maxFileSize) return;

        var backup = _filePath + ".old";
        if (File.Exists(backup)) File.Delete(backup);
        File.Move(_filePath, backup);
    }

    private static string LevelToString(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???"
    };

    public void Dispose() { }
}

internal sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        if (exception is not null)
            message = $"{message} | {exception.Message}";

        var shortCategory = category.Contains('.') ? category[(category.LastIndexOf('.') + 1)..] : category;
        provider.WriteEntry(DateTime.Now, logLevel, shortCategory, message);
    }
}
