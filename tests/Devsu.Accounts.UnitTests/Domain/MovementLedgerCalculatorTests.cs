using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Domain.Enums;
using Devsu.Accounts.Domain.Exceptions;
using Devsu.Accounts.Domain.Services;
using Devsu.Accounts.Domain.ValueObjects;

namespace Devsu.Accounts.UnitTests.Domain;

public sealed class MovementLedgerCalculatorTests
{
    private static readonly DateTimeOffset OriginalTime =
        new(2026, 2, 1, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CorrectionTime = OriginalTime.AddDays(1);

    [Fact]
    public void Recalculate_ValueChange_UpdatesAffectedSuffixAndAccountBalance()
    {
        Account account = CreateAccount(100m);
        AccountMovement first = CreateMovement(
            MovementType.Deposit,
            100m,
            200m,
            "request-1",
            OriginalTime);
        AccountMovement second = CreateMovement(
            MovementType.Withdrawal,
            -50m,
            150m,
            "request-2",
            OriginalTime.AddMinutes(1));
        first.Correct(
            MovementAmount.Create(MovementType.Deposit, 50m),
            first.OccurredAtUtc,
            CorrectionTime);

        MovementLedgerCalculator.Recalculate(
            account,
            [first, second],
            firstAffectedIndex: 0,
            CorrectionTime);

        Assert.Equal(150m, first.Balance);
        Assert.Equal(100m, second.Balance);
        Assert.Equal(100m, account.CurrentBalance);
        Assert.Equal(CorrectionTime, second.UpdatedAtUtc);
    }

    [Fact]
    public void Recalculate_WhenIntermediateBalanceIsNegative_ChangesNoBalances()
    {
        Account account = CreateAccount(100m);
        account.RebuildBalance(50m, OriginalTime.AddMinutes(2));
        AccountMovement first = CreateMovement(
            MovementType.Deposit,
            20m,
            200m,
            "request-1",
            OriginalTime);
        AccountMovement second = CreateMovement(
            MovementType.Withdrawal,
            -150m,
            50m,
            "request-2",
            OriginalTime.AddMinutes(1));

        InsufficientFundsException exception = Assert.Throws<InsufficientFundsException>(() =>
            MovementLedgerCalculator.Recalculate(
                account,
                [first, second],
                firstAffectedIndex: 0,
                CorrectionTime));

        Assert.Equal("Saldo no disponible", exception.Message);
        Assert.Equal(200m, first.Balance);
        Assert.Equal(50m, second.Balance);
        Assert.Equal(50m, account.CurrentBalance);
    }

    [Fact]
    public void Recalculate_WhenAccountIsInactive_StillRepairsLedger()
    {
        Account account = CreateAccount(100m);
        account.ChangeStatus(false, OriginalTime.AddHours(1));
        AccountMovement movement = CreateMovement(
            MovementType.Deposit,
            25m,
            125m,
            "request-1",
            OriginalTime);

        MovementLedgerCalculator.Recalculate(
            account,
            [movement],
            firstAffectedIndex: 0,
            CorrectionTime);

        Assert.Equal(125m, account.CurrentBalance);
        Assert.False(account.IsActive);
    }

    private static Account CreateAccount(decimal initialBalance)
    {
        return Account.Create(
            "001234",
            AccountType.Savings,
            initialBalance,
            true,
            Guid.NewGuid(),
            OriginalTime);
    }

    private static AccountMovement CreateMovement(
        MovementType type,
        decimal value,
        decimal balance,
        string idempotencyKey,
        DateTimeOffset occurredAtUtc)
    {
        return AccountMovement.Create(
            "001234",
            MovementAmount.Create(type, value),
            balance,
            idempotencyKey,
            new string('A', AccountMovement.RequestFingerprintLength),
            occurredAtUtc);
    }
}
