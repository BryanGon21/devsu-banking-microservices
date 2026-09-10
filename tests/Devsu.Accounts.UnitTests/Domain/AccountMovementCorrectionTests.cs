using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Domain.Enums;
using Devsu.Accounts.Domain.Exceptions;
using Devsu.Accounts.Domain.ValueObjects;

namespace Devsu.Accounts.UnitTests.Domain;

public sealed class AccountMovementCorrectionTests
{
    private static readonly DateTimeOffset OriginalTime =
        new(2026, 2, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidData_NormalizesAuditFields()
    {
        AccountMovementCorrection correction = AccountMovementCorrection.Create(
            10,
            MovementAmount.Create(MovementType.Withdrawal, -50m),
            OriginalTime,
            50m,
            MovementAmount.Create(MovementType.Withdrawal, -25m),
            OriginalTime.AddHours(-1),
            75m,
            "  Incorrect amount  ",
            "trace-123",
            OriginalTime.AddHours(1));

        Assert.Equal(10, correction.MovementId);
        Assert.Equal(-50m, correction.PreviousValue);
        Assert.Equal(-25m, correction.NewValue);
        Assert.Equal("Incorrect amount", correction.Reason);
        Assert.Equal("trace-123", correction.CorrelationId);
    }

    [Fact]
    public void Create_WithoutReason_IsRejected()
    {
        BusinessRuleException exception = Assert.Throws<BusinessRuleException>(() =>
            AccountMovementCorrection.Create(
                10,
                MovementAmount.Create(MovementType.Deposit, 50m),
                OriginalTime,
                150m,
                MovementAmount.Create(MovementType.Deposit, 25m),
                OriginalTime,
                125m,
                " ",
                "trace-123",
                OriginalTime.AddHours(1)));

        Assert.Equal("movement_correction_reason_required", exception.Code);
    }

    [Fact]
    public void Correct_WithEquivalentInstantAndSameAmount_IsNoOp()
    {
        AccountMovement movement = AccountMovement.Create(
            "001234",
            MovementAmount.Create(MovementType.Deposit, 50m),
            150m,
            "request-1",
            new string('A', AccountMovement.RequestFingerprintLength),
            OriginalTime);

        bool changed = movement.Correct(
            MovementAmount.Create(MovementType.Deposit, 50.00m),
            OriginalTime.ToOffset(TimeSpan.FromHours(-4)),
            OriginalTime.AddHours(1));

        Assert.False(changed);
        Assert.Equal(OriginalTime, movement.OccurredAtUtc);
        Assert.Equal(OriginalTime, movement.UpdatedAtUtc);
        Assert.Equal(150m, movement.Balance);
    }
}
