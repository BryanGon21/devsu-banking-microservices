using Devsu.Customers.Application.Exceptions;
using Devsu.Customers.Application.Ports;
using Devsu.Customers.Domain.Entities;
using Devsu.Customers.Domain.Events;
using Devsu.Customers.Infrastructure.Messaging.Outbox;
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

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    async Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        IHasDomainEvents[] aggregatesWithEvents = ChangeTracker
            .Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToArray();

        AddOutboxMessages(aggregatesWithEvents);

        try
        {
            int affectedRows = await base.SaveChangesAsync(
                acceptAllChangesOnSuccess,
                cancellationToken);

            foreach (IHasDomainEvents aggregate in aggregatesWithEvents)
            {
                aggregate.ClearDomainEvents();
            }

            return affectedRows;
        }
        catch (DbUpdateException exception)
            when (IsIdentificationConstraintViolation(exception))
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

    private void AddOutboxMessages(IEnumerable<IHasDomainEvents> aggregates)
    {
        HashSet<Guid> trackedEventIds = ChangeTracker
            .Entries<OutboxMessage>()
            .Select(entry => entry.Entity.EventId)
            .ToHashSet();

        OutboxMessage[] messages = aggregates
            .SelectMany(aggregate => aggregate.DomainEvents)
            .Where(domainEvent => trackedEventIds.Add(domainEvent.EventId))
            .Select(CustomerIntegrationEventMapper.Map)
            .ToArray();

        OutboxMessages.AddRange(messages);
    }

    private static bool IsIdentificationConstraintViolation(DbUpdateException exception)
    {
        return exception.GetBaseException() is SqlException { Number: 2601 or 2627 } sqlException &&
            sqlException.Message.Contains("UX_People_Identification", StringComparison.Ordinal);
    }
}
