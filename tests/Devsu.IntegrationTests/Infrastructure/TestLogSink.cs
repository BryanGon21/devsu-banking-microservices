using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Devsu.IntegrationTests.Infrastructure;

internal sealed class TestLogSink : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyCollection<string> Messages => _messages.ToArray();

    public ILogger CreateLogger(string categoryName)
    {
        return new SinkLogger(_messages);
    }

    public void Dispose()
    {
    }

    private sealed class SinkLogger : ILogger
    {
        private readonly ConcurrentQueue<string> _messages;

        public SinkLogger(ConcurrentQueue<string> messages)
        {
            _messages = messages;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _messages.Enqueue(formatter(state, exception));
        }
    }
}
