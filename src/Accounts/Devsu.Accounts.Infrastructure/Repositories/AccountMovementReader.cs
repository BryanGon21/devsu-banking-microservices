using Devsu.Accounts.Application.Ports;
using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Devsu.Accounts.Infrastructure.Repositories;

internal sealed class AccountMovementReader : IAccountMovementReader
{
    private readonly AccountsDbContext _dbContext;

    public AccountMovementReader(AccountsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AccountMovement?> GetByIdAsync(
        long movementId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.AccountMovements
            .AsNoTracking()
            .SingleOrDefaultAsync(
                movement => movement.Id == movementId,
                cancellationToken);
    }

    public async Task<(IReadOnlyCollection<AccountMovement> Items, int TotalItems)> ListAsync(
        int skip,
        int take,
        string? accountNumber,
        DateTimeOffset? startInclusiveUtc,
        DateTimeOffset? endExclusiveUtc,
        CancellationToken cancellationToken)
    {
        IQueryable<AccountMovement> query = _dbContext.AccountMovements.AsNoTracking();

        if (accountNumber is not null)
        {
            query = query.Where(movement => movement.AccountNumber == accountNumber);
        }

        if (startInclusiveUtc.HasValue)
        {
            query = query.Where(movement => movement.OccurredAtUtc >= startInclusiveUtc.Value);
        }

        if (endExclusiveUtc.HasValue)
        {
            query = query.Where(movement => movement.OccurredAtUtc < endExclusiveUtc.Value);
        }

        int totalItems = await query.CountAsync(cancellationToken);
        AccountMovement[] movements = await query
            .OrderByDescending(movement => movement.OccurredAtUtc)
            .ThenByDescending(movement => movement.Id)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);

        return (movements, totalItems);
    }
}
