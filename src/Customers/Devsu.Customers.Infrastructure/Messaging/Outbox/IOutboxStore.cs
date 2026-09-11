namespace Devsu.Customers.Infrastructure.Messaging.Outbox;

internal interface IOutboxStore
{
    public Task<IReadOnlyCollection<OutboxMessage>> ClaimPendingAsync(
        Guid leaseId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        int batchSize,
        CancellationToken cancellationToken);

    public Task<bool> MarkPublishedAsync(
        Guid eventId,
        Guid leaseId,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken);

    public Task<bool> MarkFailedAsync(
        Guid eventId,
        Guid leaseId,
        DateTimeOffset nextAttemptAtUtc,
        string error,
        CancellationToken cancellationToken);
}
