using Devsu.Accounts.Domain.Enums;
using Devsu.Accounts.Domain.Exceptions;
using Devsu.Accounts.Domain.ValueObjects;

namespace Devsu.Accounts.Domain.Entities;

public sealed class AccountMovement
{
    public const int MaximumIdempotencyKeyLength = 128;
    public const int RequestFingerprintLength = 64;

    private AccountMovement()
    {
    }

    private AccountMovement(
        string accountNumber,
        MovementAmount amount,
        decimal balance,
        string idempotencyKey,
        string requestFingerprint,
        DateTimeOffset occurredAtUtc)
    {
        AccountNumber = ValidateAccountNumber(accountNumber);
        Type = amount.Type;
        Value = amount.Value;
        Balance = ValidateBalance(balance);
        IdempotencyKey = ValidateIdempotencyKey(idempotencyKey);
        RequestFingerprint = ValidateRequestFingerprint(requestFingerprint);
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        CreatedAtUtc = OccurredAtUtc;
        UpdatedAtUtc = OccurredAtUtc;
    }

    public long Id { get; private set; }

    public string AccountNumber { get; private set; } = string.Empty;

    public MovementType Type { get; private set; }

    public decimal Value { get; private set; }

    public decimal Balance { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string RequestFingerprint { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static AccountMovement Create(
        string accountNumber,
        MovementAmount amount,
        decimal balance,
        string idempotencyKey,
        string requestFingerprint,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(amount);

        return new AccountMovement(
            accountNumber,
            amount,
            balance,
            idempotencyKey,
            requestFingerprint,
            occurredAtUtc);
    }

    public bool HasSameDetails(MovementAmount amount, DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(amount);

        return Type == amount.Type &&
            Value == amount.Value &&
            OccurredAtUtc == occurredAtUtc.ToUniversalTime();
    }

    public bool Correct(
        MovementAmount amount,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset correctedAtUtc)
    {
        if (HasSameDetails(amount, occurredAtUtc))
        {
            return false;
        }

        Type = amount.Type;
        Value = amount.Value;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        UpdatedAtUtc = correctedAtUtc.ToUniversalTime();
        return true;
    }

    public void RecalculateBalance(decimal balance, DateTimeOffset recalculatedAtUtc)
    {
        Balance = ValidateBalance(balance);
        UpdatedAtUtc = recalculatedAtUtc.ToUniversalTime();
    }

    private static string ValidateAccountNumber(string accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            throw new BusinessRuleException(
                "account_number_required",
                "Account number is required.");
        }

        string normalizedAccountNumber = accountNumber.Trim();
        if (normalizedAccountNumber.Length > Account.MaximumNumberLength ||
            normalizedAccountNumber.Any(character => !char.IsAsciiDigit(character)))
        {
            throw new BusinessRuleException(
                "account_number_invalid",
                $"Account number must contain only digits and cannot exceed {Account.MaximumNumberLength} characters.");
        }

        return normalizedAccountNumber;
    }

    private static decimal ValidateBalance(decimal balance)
    {
        if (balance < 0 ||
            balance > Account.MaximumBalance ||
            decimal.Round(balance, 2, MidpointRounding.ToEven) != balance)
        {
            throw new BusinessRuleException(
                "movement_balance_invalid",
                "Movement balance must be non-negative and fit decimal(18,2).");
        }

        return balance;
    }

    private static string ValidateIdempotencyKey(string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) ||
            idempotencyKey.Length > MaximumIdempotencyKeyLength ||
            idempotencyKey.Any(char.IsControl) ||
            !string.Equals(idempotencyKey, idempotencyKey.Trim(), StringComparison.Ordinal))
        {
            throw new BusinessRuleException(
                "idempotency_key_invalid",
                $"Idempotency key is required, cannot exceed {MaximumIdempotencyKeyLength} characters, contain control characters, or have surrounding whitespace.");
        }

        return idempotencyKey;
    }

    private static string ValidateRequestFingerprint(string requestFingerprint)
    {
        if (requestFingerprint.Length != RequestFingerprintLength ||
            requestFingerprint.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new BusinessRuleException(
                "request_fingerprint_invalid",
                "Request fingerprint is invalid.");
        }

        return requestFingerprint;
    }
}
