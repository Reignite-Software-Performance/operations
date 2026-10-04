using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Operations.Tests.Fakes;

/// <summary>A single captured log record.</summary>
public sealed record LogEntry(LogLevel Level, string Category, string Message, Exception? Exception);

/// <summary>
/// Test <see cref="ILoggerProvider"/> that captures rendered log messages so assertions
/// can inspect structured payloads written at Information level.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<LogEntry> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(CapturingLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => provider.Entries.Enqueue(new LogEntry(logLevel, category, formatter(state, exception), exception));
    }
}
