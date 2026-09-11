using Devsu.Accounts.Application.Ports;
using Devsu.Accounts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Devsu.Accounts.Infrastructure.Repositories;

internal sealed class AccountStatementReader : IAccountStatementReader
{
    private readonly AccountsDbContext _dbContext;

    public AccountStatementReader(AccountsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AccountStatementData?> GetAsync(
        Guid customerId,
        DateTimeOffset startInclusiveUtc,
        DateTimeOffset endExclusiveUtc,
        CancellationToken cancellationToken)
    {
        var customer = await _dbContext.CustomerProjections
            .AsNoTracking()
            .Where(projection => projection.CustomerId == customerId)
            .Select(projection => new
            {
                projection.CustomerId,
                projection.Name,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var accountRows = await _dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.CustomerId == customerId)
            .OrderBy(account => account.Number)
            .Select(account => new
            {
                account.Number,
                account.Type,
                account.InitialBalance,
                OpeningBalance = _dbContext.AccountMovements
                    .Where(movement =>
                        movement.AccountNumber == account.Number &&
                        movement.OccurredAtUtc < startInclusiveUtc)
                    .OrderByDescending(movement => movement.OccurredAtUtc)
                    .ThenByDescending(movement => movement.Id)
                    .Select(movement => (decimal?)movement.Balance)
                    .FirstOrDefault() ?? account.InitialBalance,
                account.CurrentBalance,
                account.IsActive,
            })
            .ToArrayAsync(cancellationToken);

        AccountStatementMovementData[] movementRows = await (
            from movement in _dbContext.AccountMovements.AsNoTracking()
            join account in _dbContext.Accounts.AsNoTracking()
                on movement.AccountNumber equals account.Number
            where account.CustomerId == customerId &&
                movement.OccurredAtUtc >= startInclusiveUtc &&
                movement.OccurredAtUtc < endExclusiveUtc
            orderby movement.AccountNumber, movement.OccurredAtUtc, movement.Id
            select new AccountStatementMovementData(
                movement.Id,
                movement.AccountNumber,
                movement.OccurredAtUtc,
                movement.Type,
                movement.Value,
                movement.Balance))
            .ToArrayAsync(cancellationToken);

        ILookup<string, AccountStatementMovementData> movementsByAccount = movementRows
            .ToLookup(movement => movement.AccountNumber, StringComparer.Ordinal);
        AccountStatementAccountData[] accounts = accountRows
            .Select(account => new AccountStatementAccountData(
                account.Number,
                account.Type,
                account.InitialBalance,
                account.OpeningBalance,
                account.CurrentBalance,
                account.IsActive,
                movementsByAccount[account.Number].ToArray()))
            .ToArray();

        return new AccountStatementData(customer.CustomerId, customer.Name, accounts);
    }
}
