using Devsu.Accounts.Application.Exceptions;
using Devsu.Accounts.Application.Ports;
using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Infrastructure.Messaging.Inbox;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Devsu.Accounts.Infrastructure.Persistence;

public sealed class AccountsDbContext : DbContext, IUnitOfWork
{
    public AccountsDbContext(DbContextOptions<AccountsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<CustomerProjection> CustomerProjections => Set<CustomerProjection>();

    internal DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    async Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsAccountNumberConstraintViolation(exception))
        {
            throw new ConflictException(
                "account_duplicate_number",
                "An account with the specified number already exists.",
                exception);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConflictException(
                "account_concurrency_conflict",
                "The account was modified by another operation. Refresh the resource and retry.",
                exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("accounts");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccountsDbContext).Assembly);
    }

    private static bool IsAccountNumberConstraintViolation(DbUpdateException exception)
    {
        return exception.GetBaseException() is SqlException { Number: 2601 or 2627 } sqlException &&
            sqlException.Message.Contains("PK_Accounts", StringComparison.Ordinal);
    }
}
