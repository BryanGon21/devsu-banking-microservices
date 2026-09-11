namespace Devsu.Accounts.Infrastructure.Messaging.Inbox;

internal sealed class InboxMessage
{
    public const int MaximumEventTypeLength = 128;
    public const int MaximumCorrelationIdLength = 128;

    private InboxMessage()
    {
    }

    private InboxMessage(
        Guid eventId,
        string eventType,
        string correlationId,
        Guid aggregateId,
        long aggregateVersion,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset processedAtUtc)
    {
        EventId = eventId;
        EventType = eventType;
        CorrelationId = correlationId;
        AggregateId = aggregateId;
        AggregateVersion = aggregateVersion;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        ProcessedAtUtc = processedAtUtc.ToUniversalTime();
    }

    public Guid EventId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string CorrelationId { get; private set; } = string.Empty;

    public Guid AggregateId { get; private set; }

    public long AggregateVersion { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public DateTimeOffset ProcessedAtUtc { get; private set; }

    public static InboxMessage Create(
        Guid eventId,
        string eventType,
        string correlationId,
        Guid aggregateId,
        long aggregateVersion,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset processedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        if (eventId == Guid.Empty || aggregateId == Guid.Empty || aggregateVersion < 1)
        {
            throw new ArgumentException("Inbox message identifiers and aggregate version must be valid.");
        }

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

        return new InboxMessage(
            eventId,
            eventType,
            correlationId,
            aggregateId,
            aggregateVersion,
            occurredAtUtc,
            processedAtUtc);
    }
}
