using Devsu.Accounts.Application.Ports;
using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Devsu.Accounts.Infrastructure.Repositories;

internal sealed class AccountRepository : IAccountRepository
{
    private readonly AccountsDbContext _dbContext;

    public AccountRepository(AccountsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Account account, CancellationToken cancellationToken)
    {
        await _dbContext.Accounts.AddAsync(account, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string number, CancellationToken cancellationToken)
    {
        return await _dbContext.Accounts.AnyAsync(
            account => account.Number == number,
            cancellationToken);
    }

    public async Task<Account?> GetByNumberAsync(
        string number,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Accounts.SingleOrDefaultAsync(
            account => account.Number == number,
            cancellationToken);
    }

    public async Task<(IReadOnlyCollection<Account> Items, int TotalItems)> ListAsync(
        int skip,
        int take,
        Guid? customerId,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        IQueryable<Account> query = _dbContext.Accounts.AsNoTracking();

        if (customerId.HasValue)
        {
            query = query.Where(account => account.CustomerId == customerId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(account => account.IsActive == isActive.Value);
        }

        int totalItems = await query.CountAsync(cancellationToken);
        Account[] accounts = await query
            .OrderBy(account => account.Number)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);

        return (accounts, totalItems);
    }
}
