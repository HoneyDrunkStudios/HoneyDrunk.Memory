using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Memory.Tests;

/// <summary>Captures log entries, with their exceptions, for assertions.</summary>
/// <typeparam name="T">The category type.</typeparam>
internal sealed class ListLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel level, string message, Exception? exception)> entries = [];

    /// <summary>Gets the captured entries.</summary>
    public IReadOnlyList<(LogLevel level, string message, Exception? exception)> Entries
    {
        get
        {
            lock (this.entries)
            {
                return [.. this.entries];
            }
        }
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (this.entries)
        {
            this.entries.Add((logLevel, formatter(state, exception), exception));
        }
    }
}
