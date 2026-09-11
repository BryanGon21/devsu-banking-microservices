using Devsu.Customers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Devsu.Customers.Infrastructure.Messaging.Outbox;

internal sealed class OutboxStore : IOutboxStore
{
    private readonly CustomersDbContext _dbContext;

    public OutboxStore(CustomersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<OutboxMessage>> ClaimPendingAsync(
        Guid leaseId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        int batchSize,
        CancellationToken cancellationToken)
    {
        DateTimeOffset leasedUntilUtc = nowUtc.Add(leaseDuration);
        Guid[] candidateIds = await _dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message =>
                message.PublishedAtUtc == null &&
                message.NextAttemptAtUtc <= nowUtc &&
                (message.LeasedUntilUtc == null || message.LeasedUntilUtc <= nowUtc))
            .OrderBy(message => message.OccurredAtUtc)
            .ThenBy(message => message.EventId)
            .Select(message => message.EventId)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);

        List<OutboxMessage> claimedMessages = new(candidateIds.Length);
        foreach (Guid eventId in candidateIds)
        {
            int affectedRows = await _dbContext.OutboxMessages
                .Where(message =>
                    message.EventId == eventId &&
                    message.PublishedAtUtc == null &&
                    message.NextAttemptAtUtc <= nowUtc &&
                    (message.LeasedUntilUtc == null || message.LeasedUntilUtc <= nowUtc))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(message => message.LeaseId, (Guid?)leaseId)
                        .SetProperty(message => message.LeasedUntilUtc, (DateTimeOffset?)leasedUntilUtc)
                        .SetProperty(message => message.LastAttemptAtUtc, (DateTimeOffset?)nowUtc)
                        .SetProperty(message => message.AttemptCount, message => message.AttemptCount + 1),
                    cancellationToken);

            if (affectedRows == 0)
            {
                continue;
            }

            OutboxMessage claimedMessage = await _dbContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message => message.EventId == eventId && message.LeaseId == leaseId,
                    cancellationToken);

            claimedMessages.Add(claimedMessage);
        }

        return claimedMessages;
    }

    public async Task<bool> MarkPublishedAsync(
        Guid eventId,
        Guid leaseId,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken)
    {
        int affectedRows = await _dbContext.OutboxMessages
            .Where(message =>
                message.EventId == eventId &&
                message.LeaseId == leaseId &&
                message.PublishedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.PublishedAtUtc, (DateTimeOffset?)publishedAtUtc)
                    .SetProperty(message => message.LeaseId, (Guid?)null)
                    .SetProperty(message => message.LeasedUntilUtc, (DateTimeOffset?)null)
                    .SetProperty(message => message.LastError, (string?)null),
                cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> MarkFailedAsync(
        Guid eventId,
        Guid leaseId,
        DateTimeOffset nextAttemptAtUtc,
        string error,
        CancellationToken cancellationToken)
    {
        int affectedRows = await _dbContext.OutboxMessages
            .Where(message =>
                message.EventId == eventId &&
                message.LeaseId == leaseId &&
                message.PublishedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.NextAttemptAtUtc, nextAttemptAtUtc)
                    .SetProperty(message => message.LeaseId, (Guid?)null)
                    .SetProperty(message => message.LeasedUntilUtc, (DateTimeOffset?)null)
                    .SetProperty(message => message.LastError, error),
                cancellationToken);

        return affectedRows == 1;
    }
}
