using Devsu.Customers.Infrastructure.Messaging.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Devsu.Customers.UnitTests.Infrastructure;

public sealed class OutboxProcessorTests
{
    private static readonly DateTimeOffset CurrentTime =
        new(2026, 2, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProcessBatch_WhenBrokerIsUnavailable_KeepsMessagePendingForRetry()
    {
        OutboxMessage message = OutboxMessage.Create(
            Guid.NewGuid(),
            "customer.created.v1",
            CurrentTime,
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid(),
            1,
            "{}");
        FakeOutboxStore store = new(message);
        OutboxPublisherOptions options = new()
        {
            BaseRetryDelay = TimeSpan.FromSeconds(2),
            MaximumRetryDelay = TimeSpan.FromMinutes(5),
        };
        OutboxProcessor processor = new(
            store,
            new UnavailablePublisher(),
            Options.Create(options),
            new FixedTimeProvider(CurrentTime),
            NullLogger<OutboxProcessor>.Instance);

        int processedCount = await processor.ProcessBatchAsync(CancellationToken.None);

        Assert.Equal(1, processedCount);
        Assert.False(store.MarkedAsPublished);
        Assert.True(store.MarkedAsFailed);
        Assert.Equal(CurrentTime.AddSeconds(2), store.NextAttemptAtUtc);
        Assert.Contains("InvalidOperationException: broker unavailable", store.Error, StringComparison.Ordinal);
        Assert.Null(message.PublishedAtUtc);
    }

    private sealed class FakeOutboxStore : IOutboxStore
    {
        private readonly OutboxMessage _message;
        private Guid _leaseId;

        public FakeOutboxStore(OutboxMessage message)
        {
            _message = message;
        }

        public bool MarkedAsPublished { get; private set; }

        public bool MarkedAsFailed { get; private set; }

        public DateTimeOffset? NextAttemptAtUtc { get; private set; }

        public string? Error { get; private set; }

        public Task<IReadOnlyCollection<OutboxMessage>> ClaimPendingAsync(
            Guid leaseId,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            int batchSize,
            CancellationToken cancellationToken)
        {
            _leaseId = leaseId;
            IReadOnlyCollection<OutboxMessage> messages = [_message];
            return Task.FromResult(messages);
        }

        public Task<bool> MarkPublishedAsync(
            Guid eventId,
            Guid leaseId,
            DateTimeOffset publishedAtUtc,
            CancellationToken cancellationToken)
        {
            MarkedAsPublished = true;
            return Task.FromResult(false);
        }

        public Task<bool> MarkFailedAsync(
            Guid eventId,
            Guid leaseId,
            DateTimeOffset nextAttemptAtUtc,
            string error,
            CancellationToken cancellationToken)
        {
            Assert.Equal(_leaseId, leaseId);
            Assert.Equal(_message.EventId, eventId);
            MarkedAsFailed = true;
            NextAttemptAtUtc = nextAttemptAtUtc;
            Error = error;
            return Task.FromResult(true);
        }
    }

    private sealed class UnavailablePublisher : IIntegrationEventPublisher
    {
        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("broker unavailable");
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
