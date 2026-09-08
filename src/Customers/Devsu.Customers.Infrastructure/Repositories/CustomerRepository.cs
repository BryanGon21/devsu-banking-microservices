using Devsu.Customers.Application.Ports;
using Devsu.Customers.Domain.Entities;
using Devsu.Customers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Devsu.Customers.Infrastructure.Repositories;

internal sealed class CustomerRepository : ICustomerRepository
{
    private readonly CustomersDbContext _dbContext;

    public CustomerRepository(CustomersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Customer?> GetByIdAsync(
        Guid customerId,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        IQueryable<Customer> query = _dbContext.Customers;
        if (!includeDeleted)
        {
            query = query.Where(customer => customer.DeletedAtUtc == null);
        }

        return await query.SingleOrDefaultAsync(customer => customer.Id == customerId, cancellationToken);
    }

    public async Task<(IReadOnlyCollection<Customer> Items, int Total)> ListAsync(
        int skip,
        int take,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        IQueryable<Customer> query = _dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.DeletedAtUtc == null);

        if (isActive.HasValue)
        {
            query = query.Where(customer => customer.IsActive == isActive.Value);
        }

        int total = await query.CountAsync(cancellationToken);
        Customer[] items = await query
            .OrderBy(customer => customer.Name)
            .ThenBy(customer => customer.Id)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);

        return (items, total);
    }

    public Task<bool> IdentificationExistsAsync(
        string identification,
        Guid? excludedCustomerId,
        CancellationToken cancellationToken)
    {
        return _dbContext.People.AnyAsync(
            person =>
                person.Identification == identification &&
                (!excludedCustomerId.HasValue || person.Id != excludedCustomerId.Value),
            cancellationToken);
    }

    public void Add(Customer customer)
    {
        _dbContext.Customers.Add(customer);
    }
}
