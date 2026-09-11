using Devsu.Accounts.Application.Ports;
using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Devsu.Accounts.Infrastructure.Repositories;

internal sealed class CustomerProjectionReader : ICustomerProjectionReader
{
    private readonly AccountsDbContext _dbContext;

    public CustomerProjectionReader(AccountsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CustomerProjection?> GetByIdAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.CustomerProjections
            .AsNoTracking()
            .SingleOrDefaultAsync(customer => customer.CustomerId == customerId, cancellationToken);
    }
}
