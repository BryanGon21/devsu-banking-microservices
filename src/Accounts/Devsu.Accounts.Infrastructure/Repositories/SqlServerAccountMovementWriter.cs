using Devsu.Accounts.Application.Exceptions;
using Devsu.Accounts.Application.Ports;
using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Domain.ValueObjects;
using Devsu.Accounts.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Devsu.Accounts.Infrastructure.Repositories;

internal sealed class SqlServerAccountMovementWriter : IAccountMovementWriter
{
    private readonly AccountsDbContext _dbContext;

    public SqlServerAccountMovementWriter(AccountsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AccountMovementWriteResult> CreateAsync(
        string accountNumber,
        MovementAmount amount,
        string idempotencyKey,
        string requestFingerprint,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                _dbContext.ChangeTracker.Clear();

                await using IDbContextTransaction transaction =
                    await _dbContext.Database.BeginTransactionAsync(cancellationToken);

                AccountMovement? existingMovement = await FindByIdempotencyKeyAsync(
                    idempotencyKey,
                    cancellationToken);
                if (existingMovement is not null)
                {
                    return ResolveExisting(existingMovement, requestFingerprint);
                }

                Account? account = await _dbContext.Accounts
                    .FromSqlInterpolated(
                        $"SELECT * FROM [accounts].[Accounts] WITH (UPDLOCK, ROWLOCK) WHERE [Number] = {accountNumber}")
                    .SingleOrDefaultAsync(cancellationToken);
                if (account is null)
                {
                    throw new NotFoundException(
                        "account_not_found",
                        "The requested account does not exist.");
                }

                decimal resultingBalance = account.ApplyMovement(amount, occurredAtUtc);
                AccountMovement movement = AccountMovement.Create(
                    account.Number,
                    amount,
                    resultingBalance,
                    idempotencyKey,
                    requestFingerprint,
                    occurredAtUtc);

                await _dbContext.AccountMovements.AddAsync(movement, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new AccountMovementWriteResult(movement, WasCreated: true);
            });
        }
        catch (DbUpdateException exception) when (IsIdempotencyKeyConstraintViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();
            AccountMovement? existingMovement = await FindByIdempotencyKeyAsync(
                idempotencyKey,
                cancellationToken);
            if (existingMovement is null)
            {
                throw;
            }

            return ResolveExisting(existingMovement, requestFingerprint);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConflictException(
                "account_concurrency_conflict",
                "The account was modified by another operation. Retry with a new request if needed.",
                exception);
        }
    }

    private async Task<AccountMovement?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await _dbContext.AccountMovements
            .AsNoTracking()
            .SingleOrDefaultAsync(
                movement => movement.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    private static AccountMovementWriteResult ResolveExisting(
        AccountMovement existingMovement,
        string requestFingerprint)
    {
        if (!string.Equals(
                existingMovement.RequestFingerprint,
                requestFingerprint,
                StringComparison.Ordinal))
        {
            throw new ConflictException(
                "idempotency_key_reused",
                "Idempotency-Key was already used with a different request payload.");
        }

        return new AccountMovementWriteResult(existingMovement, WasCreated: false);
    }

    private static bool IsIdempotencyKeyConstraintViolation(DbUpdateException exception)
    {
        return exception.GetBaseException() is SqlException { Number: 2601 or 2627 } sqlException &&
            sqlException.Message.Contains(
                "UX_AccountMovements_IdempotencyKey",
                StringComparison.Ordinal);
    }
}
