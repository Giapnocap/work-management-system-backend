using Microsoft.Extensions.Logging;

namespace WorkManagementSystem.Tests.TestSupport;

public sealed class CollectingLogger<T> : ILogger<T>
{
    private readonly List<CollectedLogEntry> _entries = new();

    public IReadOnlyList<CollectedLogEntry> Entries => _entries;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
        => EmptyScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var properties = state is IEnumerable<KeyValuePair<string, object?>> values
            ? values.ToDictionary(item => item.Key, item => item.Value)
            : new Dictionary<string, object?>();

        _entries.Add(new CollectedLogEntry(
            logLevel,
            eventId,
            formatter(state, exception),
            exception,
            properties));
    }

    private sealed class EmptyScope : IDisposable
    {
        public static readonly EmptyScope Instance = new();

        public void Dispose()
        {
        }
    }
}

public sealed record CollectedLogEntry(
    LogLevel Level,
    EventId EventId,
    string Message,
    Exception? Exception,
    IReadOnlyDictionary<string, object?> Properties);
