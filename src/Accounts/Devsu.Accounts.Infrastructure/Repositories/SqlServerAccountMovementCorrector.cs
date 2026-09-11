using Devsu.Accounts.Application.Exceptions;
using Devsu.Accounts.Application.Ports;
using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Domain.Services;
using Devsu.Accounts.Domain.ValueObjects;
using Devsu.Accounts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Devsu.Accounts.Infrastructure.Repositories;

internal sealed class SqlServerAccountMovementCorrector : IAccountMovementCorrector
{
    private readonly AccountsDbContext _dbContext;

    public SqlServerAccountMovementCorrector(AccountsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AccountMovementCorrectionResult> CorrectAsync(
        long movementId,
        MovementAmount amount,
        DateTimeOffset occurredAtUtc,
        string reason,
        string correlationId,
        DateTimeOffset correctedAtUtc,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                _dbContext.ChangeTracker.Clear();

                AccountMovement? candidate = await _dbContext.AccountMovements
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        movement => movement.Id == movementId,
                        cancellationToken);
                if (candidate is null)
                {
                    throw new NotFoundException(
                        "movement_not_found",
                        "The requested movement does not exist.");
                }

                await using IDbContextTransaction transaction =
                    await _dbContext.Database.BeginTransactionAsync(cancellationToken);

                Account? account = await _dbContext.Accounts
                    .FromSqlInterpolated(
                        $"SELECT * FROM [accounts].[Accounts] WITH (UPDLOCK, ROWLOCK) WHERE [Number] = {candidate.AccountNumber}")
                    .SingleOrDefaultAsync(cancellationToken);
                if (account is null)
                {
                    throw new NotFoundException(
                        "account_not_found",
                        "The movement account does not exist.");
                }

                AccountMovement? target = await _dbContext.AccountMovements
                    .SingleOrDefaultAsync(
                        movement => movement.Id == movementId,
                        cancellationToken);
                if (target is null)
                {
                    throw new NotFoundException(
                        "movement_not_found",
                        "The requested movement does not exist.");
                }

                if (target.HasSameDetails(amount, occurredAtUtc))
                {
                    await transaction.CommitAsync(cancellationToken);
                    return new AccountMovementCorrectionResult(target, WasCorrected: false);
                }

                List<AccountMovement> originalOrder = await _dbContext.AccountMovements
                    .Where(movement => movement.AccountNumber == account.Number)
                    .OrderBy(movement => movement.OccurredAtUtc)
                    .ThenBy(movement => movement.Id)
                    .ToListAsync(cancellationToken);
                int originalIndex = originalOrder.FindIndex(movement => movement.Id == movementId);
                if (originalIndex < 0)
                {
                    throw new NotFoundException(
                        "movement_not_found",
                        "The requested movement does not exist.");
                }

                MovementAmount previousAmount = MovementAmount.Create(target.Type, target.Value);
                DateTimeOffset previousOccurredAtUtc = target.OccurredAtUtc;
                decimal previousBalance = target.Balance;

                target.Correct(amount, occurredAtUtc, correctedAtUtc);

                List<AccountMovement> correctedOrder = originalOrder
                    .OrderBy(movement => movement.OccurredAtUtc)
                    .ThenBy(movement => movement.Id)
                    .ToList();
                int correctedIndex = correctedOrder.FindIndex(movement => movement.Id == movementId);
                int firstAffectedIndex = Math.Min(originalIndex, correctedIndex);

                MovementLedgerCalculator.Recalculate(
                    account,
                    correctedOrder,
                    firstAffectedIndex,
                    correctedAtUtc);

                AccountMovementCorrection correction = AccountMovementCorrection.Create(
                    movementId,
                    previousAmount,
                    previousOccurredAtUtc,
                    previousBalance,
                    amount,
                    target.OccurredAtUtc,
                    target.Balance,
                    reason,
                    correlationId,
                    correctedAtUtc);

                await _dbContext.AccountMovementCorrections.AddAsync(
                    correction,
                    cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new AccountMovementCorrectionResult(target, WasCorrected: true);
            });
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConflictException(
                "movement_concurrency_conflict",
                "The movement ledger was modified by another operation. Refresh and retry.",
                exception);
        }
    }
}
