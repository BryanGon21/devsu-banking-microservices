using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Infrastructure.Messaging.Inbox;
using Devsu.Accounts.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Devsu.Accounts.Infrastructure.Messaging.CustomerEvents;

internal sealed class CustomerIntegrationEventHandler
{
    private readonly AccountsDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public CustomerIntegrationEventHandler(
        AccountsDbContext dbContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<CustomerEventProcessingResult> HandleAsync(
        CustomerIntegrationEventEnvelope integrationEvent,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();

            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            bool alreadyProcessed = await _dbContext.InboxMessages.AnyAsync(
                message => message.EventId == integrationEvent.EventId,
                cancellationToken);
            if (alreadyProcessed)
            {
                return CustomerEventProcessingResult.IgnoredAsDuplicate;
            }

            CustomerProjection? projection = await _dbContext.CustomerProjections.SingleOrDefaultAsync(
                customer => customer.CustomerId == integrationEvent.Data.CustomerId,
                cancellationToken);

            bool wasApplied;
            if (projection is null)
            {
                projection = CustomerProjection.Create(
                    integrationEvent.Data.CustomerId,
                    integrationEvent.Data.Name,
                    integrationEvent.Data.IsActive,
                    integrationEvent.Data.IsDeleted,
                    integrationEvent.AggregateVersion,
                    integrationEvent.OccurredAtUtc);
                await _dbContext.CustomerProjections.AddAsync(projection, cancellationToken);
                wasApplied = true;
            }
            else
            {
                wasApplied = projection.ApplySnapshot(
                    integrationEvent.Data.Name,
                    integrationEvent.Data.IsActive,
                    integrationEvent.Data.IsDeleted,
                    integrationEvent.AggregateVersion,
                    integrationEvent.OccurredAtUtc);
            }

            InboxMessage inboxMessage = InboxMessage.Create(
                integrationEvent.EventId,
                integrationEvent.EventType,
                integrationEvent.CorrelationId,
                integrationEvent.AggregateId,
                integrationEvent.AggregateVersion,
                integrationEvent.OccurredAtUtc,
                _timeProvider.GetUtcNow());
            await _dbContext.InboxMessages.AddAsync(inboxMessage, cancellationToken);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsInboxDuplicate(exception))
            {
                await transaction.RollbackAsync(cancellationToken);
                return CustomerEventProcessingResult.IgnoredAsDuplicate;
            }

            return wasApplied
                ? CustomerEventProcessingResult.Applied
                : CustomerEventProcessingResult.IgnoredAsStale;
        });
    }

    private static bool IsInboxDuplicate(DbUpdateException exception)
    {
        return exception.GetBaseException() is SqlException { Number: 2601 or 2627 } sqlException &&
            sqlException.Message.Contains("PK_InboxMessages", StringComparison.Ordinal);
    }
}
