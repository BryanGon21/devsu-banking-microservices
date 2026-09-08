using Devsu.Customers.Application.Exceptions;
using Devsu.Customers.Application.Ports;
using Devsu.Customers.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Devsu.Customers.Infrastructure.Persistence;

public sealed class CustomersDbContext : DbContext, IUnitOfWork
{
    public CustomersDbContext(DbContextOptions<CustomersDbContext> options)
        : base(options)
    {
    }

    public DbSet<Person> People => Set<Person>();

    public DbSet<Customer> Customers => Set<Customer>();

    async Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException(
                "customer_duplicate_identification",
                "A customer with the specified identification already exists.",
                exception);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConflictException(
                "customer_concurrency_conflict",
                "The customer was modified by another operation. Refresh the resource and retry.",
                exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("customers");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomersDbContext).Assembly);
    }
}
