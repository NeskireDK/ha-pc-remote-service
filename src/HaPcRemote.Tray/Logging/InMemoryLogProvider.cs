using Microsoft.Extensions.Logging;

namespace HaPcRemote.Tray.Logging;

// Simple log entry record
internal sealed record LogEntry(DateTime Timestamp, LogLevel Level, string Category, string Message);

// Logger provider that stores entries in a ring buffer
internal sealed class InMemoryLogProvider : ILoggerProvider
{
    private readonly int _maxEntries;
    private readonly List<LogEntry> _entries = [];
    private readonly Lock _lock = new();

    public event Action<LogEntry>? OnLogEntry;

    public InMemoryLogProvider(int maxEntries = 500)
    {
        _maxEntries = maxEntries;
    }

    public IReadOnlyList<LogEntry> GetEntries()
    {
        lock (_lock)
        {
            return _entries.ToList();
        }
    }

    internal void AddEntry(LogEntry entry)
    {
        lock (_lock)
        {
            if (_entries.Count >= _maxEntries)
                _entries.RemoveAt(0);
            _entries.Add(entry);
        }
        OnLogEntry?.Invoke(entry);
    }

    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(this, categoryName);

    public void Dispose() { }
}

// Logger that writes to the provider's buffer
internal sealed class InMemoryLogger(InMemoryLogProvider provider, string category) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        if (exception is not null)
            message = $"{message} | {exception.Message}";

        // Shorten category name to just the class name
        var shortCategory = category.Contains('.') ? category[(category.LastIndexOf('.') + 1)..] : category;

        provider.AddEntry(new LogEntry(DateTime.Now, logLevel, shortCategory, message));
    }
}
