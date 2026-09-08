namespace Devsu.Customers.Infrastructure.Messaging.Outbox;

internal sealed class OutboxMessage
{
    public const int MaximumEventTypeLength = 128;
    public const int MaximumCorrelationIdLength = 128;
    public const int MaximumErrorLength = 2_000;

    private OutboxMessage()
    {
    }

    private OutboxMessage(
        Guid eventId,
        string eventType,
        DateTimeOffset occurredAtUtc,
        string correlationId,
        Guid aggregateId,
        long aggregateVersion,
        string payload)
    {
        EventId = eventId;
        EventType = eventType;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        CorrelationId = correlationId;
        AggregateId = aggregateId;
        AggregateVersion = aggregateVersion;
        Payload = payload;
        NextAttemptAtUtc = OccurredAtUtc;
    }

    public Guid EventId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;

    public Guid AggregateId { get; private set; }

    public long AggregateVersion { get; private set; }

    public string Payload { get; private set; } = string.Empty;

    public int AttemptCount { get; private set; }

    public DateTimeOffset NextAttemptAtUtc { get; private set; }

    public DateTimeOffset? LastAttemptAtUtc { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public Guid? LeaseId { get; private set; }

    public DateTimeOffset? LeasedUntilUtc { get; private set; }

    public string? LastError { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static OutboxMessage Create(
        Guid eventId,
        string eventType,
        DateTimeOffset occurredAtUtc,
        string correlationId,
        Guid aggregateId,
        long aggregateVersion,
        string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        if (eventType.Length > MaximumEventTypeLength)
        {
            throw new ArgumentException(
                $"Event type cannot exceed {MaximumEventTypeLength} characters.",
                nameof(eventType));
        }

        if (correlationId.Length > MaximumCorrelationIdLength)
        {
            throw new ArgumentException(
                $"Correlation ID cannot exceed {MaximumCorrelationIdLength} characters.",
                nameof(correlationId));
        }

        if (eventId == Guid.Empty || aggregateId == Guid.Empty || aggregateVersion < 1)
        {
            throw new ArgumentException("Outbox message identifiers and aggregate version must be valid.");
        }

        return new OutboxMessage(
            eventId,
            eventType,
            occurredAtUtc,
            correlationId,
            aggregateId,
            aggregateVersion,
            payload);
    }
}
