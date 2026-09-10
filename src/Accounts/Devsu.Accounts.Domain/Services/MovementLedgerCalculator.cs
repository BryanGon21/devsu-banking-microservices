using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Domain.Exceptions;

namespace Devsu.Accounts.Domain.Services;

public static class MovementLedgerCalculator
{
    public static void Recalculate(
        Account account,
        IReadOnlyList<AccountMovement> chronologicalMovements,
        int firstAffectedIndex,
        DateTimeOffset recalculatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(chronologicalMovements);

        if (firstAffectedIndex < 0 || firstAffectedIndex >= chronologicalMovements.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(firstAffectedIndex));
        }

        decimal runningBalance = account.InitialBalance;
        decimal[] recalculatedBalances = new decimal[chronologicalMovements.Count - firstAffectedIndex];

        for (int index = 0; index < chronologicalMovements.Count; index++)
        {
            AccountMovement movement = chronologicalMovements[index];
            if (!string.Equals(
                    movement.AccountNumber,
                    account.Number,
                    StringComparison.Ordinal))
            {
                throw new BusinessRuleException(
                    "movement_account_mismatch",
                    "Every movement in the ledger must belong to the same account.");
            }

            runningBalance += movement.Value;
            if (runningBalance < 0)
            {
                throw new InsufficientFundsException();
            }

            if (runningBalance > Account.MaximumBalance)
            {
                throw new BusinessRuleException(
                    "account_balance_limit_exceeded",
                    "The recalculated balance exceeds the supported decimal(18,2) limit.");
            }

            if (index >= firstAffectedIndex)
            {
                recalculatedBalances[index - firstAffectedIndex] = runningBalance;
            }
        }

        for (int index = firstAffectedIndex; index < chronologicalMovements.Count; index++)
        {
            chronologicalMovements[index].RecalculateBalance(
                recalculatedBalances[index - firstAffectedIndex],
                recalculatedAtUtc);
        }

        account.RebuildBalance(runningBalance, recalculatedAtUtc);
    }
}
