using Devsu.Accounts.Domain.Enums;
using Devsu.Accounts.Domain.Exceptions;
using Devsu.Accounts.Domain.ValueObjects;

namespace Devsu.Accounts.Domain.Entities;

public sealed class AccountMovementCorrection
{
    public const int MaximumReasonLength = 500;
    public const int MaximumCorrelationIdLength = 128;

    private AccountMovementCorrection()
    {
    }

    private AccountMovementCorrection(
        long movementId,
        MovementAmount previousAmount,
        DateTimeOffset previousOccurredAtUtc,
        decimal previousBalance,
        MovementAmount newAmount,
        DateTimeOffset newOccurredAtUtc,
        decimal newBalance,
        string reason,
        string correlationId,
        DateTimeOffset correctedAtUtc)
    {
        if (movementId < 1)
        {
            throw new BusinessRuleException(
                "movement_id_invalid",
                "Movement identifier must be greater than zero.");
        }

        MovementId = movementId;
        PreviousType = previousAmount.Type;
        PreviousValue = previousAmount.Value;
        PreviousOccurredAtUtc = previousOccurredAtUtc.ToUniversalTime();
        PreviousBalance = ValidateBalance(previousBalance);
        NewType = newAmount.Type;
        NewValue = newAmount.Value;
        NewOccurredAtUtc = newOccurredAtUtc.ToUniversalTime();
        NewBalance = ValidateBalance(newBalance);
        Reason = ValidateReason(reason);
        CorrelationId = ValidateCorrelationId(correlationId);
        CorrectedAtUtc = correctedAtUtc.ToUniversalTime();
    }

    public long Id { get; private set; }

    public long MovementId { get; private set; }

    public MovementType PreviousType { get; private set; }

    public decimal PreviousValue { get; private set; }

    public DateTimeOffset PreviousOccurredAtUtc { get; private set; }

    public decimal PreviousBalance { get; private set; }

    public MovementType NewType { get; private set; }

    public decimal NewValue { get; private set; }

    public DateTimeOffset NewOccurredAtUtc { get; private set; }

    public decimal NewBalance { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public string CorrelationId { get; private set; } = string.Empty;

    public DateTimeOffset CorrectedAtUtc { get; private set; }

    public static AccountMovementCorrection Create(
        long movementId,
        MovementAmount previousAmount,
        DateTimeOffset previousOccurredAtUtc,
        decimal previousBalance,
        MovementAmount newAmount,
        DateTimeOffset newOccurredAtUtc,
        decimal newBalance,
        string reason,
        string correlationId,
        DateTimeOffset correctedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(previousAmount);
        ArgumentNullException.ThrowIfNull(newAmount);

        return new AccountMovementCorrection(
            movementId,
            previousAmount,
            previousOccurredAtUtc,
            previousBalance,
            newAmount,
            newOccurredAtUtc,
            newBalance,
            reason,
            correlationId,
            correctedAtUtc);
    }

    private static decimal ValidateBalance(decimal balance)
    {
        if (balance < 0 ||
            balance > Account.MaximumBalance ||
            decimal.Round(balance, 2, MidpointRounding.ToEven) != balance)
        {
            throw new BusinessRuleException(
                "movement_correction_balance_invalid",
                "Correction balance must be non-negative and fit decimal(18,2).");
        }

        return balance;
    }

    private static string ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessRuleException(
                "movement_correction_reason_required",
                "Correction reason is required.");
        }

        string normalizedReason = reason.Trim();
        if (normalizedReason.Length > MaximumReasonLength)
        {
            throw new BusinessRuleException(
                "movement_correction_reason_too_long",
                $"Correction reason cannot exceed {MaximumReasonLength} characters.");
        }

        return normalizedReason;
    }

    private static string ValidateCorrelationId(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId) ||
            correlationId.Length > MaximumCorrelationIdLength ||
            correlationId.Any(char.IsControl))
        {
            throw new BusinessRuleException(
                "correlation_id_invalid",
                $"Correlation identifier is required, cannot exceed {MaximumCorrelationIdLength} characters, or contain control characters.");
        }

        return correlationId;
    }
}
