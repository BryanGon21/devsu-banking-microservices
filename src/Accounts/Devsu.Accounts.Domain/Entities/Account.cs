using Devsu.Accounts.Domain.Enums;
using Devsu.Accounts.Domain.Exceptions;
using Devsu.Accounts.Domain.ValueObjects;

namespace Devsu.Accounts.Domain.Entities;

public sealed class Account
{
    public const int MaximumNumberLength = 20;
    public const decimal MaximumBalance = 9_999_999_999_999_999.99m;

    private Account()
    {
    }

    private Account(
        string number,
        AccountType type,
        decimal initialBalance,
        bool isActive,
        Guid customerId,
        DateTimeOffset createdAtUtc)
    {
        Number = ValidateNumber(number);
        Type = ValidateType(type);
        InitialBalance = ValidateBalance(initialBalance, nameof(initialBalance));
        CurrentBalance = InitialBalance;
        IsActive = isActive;
        CustomerId = ValidateCustomerId(customerId);
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
    }

    public string Number { get; private set; } = string.Empty;

    public AccountType Type { get; private set; }

    public decimal InitialBalance { get; private set; }

    public decimal CurrentBalance { get; private set; }

    public bool IsActive { get; private set; }

    public Guid CustomerId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static Account Create(
        string number,
        AccountType type,
        decimal initialBalance,
        bool isActive,
        Guid customerId,
        DateTimeOffset createdAtUtc)
    {
        return new Account(number, type, initialBalance, isActive, customerId, createdAtUtc);
    }

    public bool Update(
        AccountType type,
        decimal initialBalance,
        bool isActive,
        bool hasMovements,
        DateTimeOffset updatedAtUtc)
    {
        AccountType validatedType = ValidateType(type);
        decimal validatedInitialBalance = ValidateBalance(initialBalance, nameof(initialBalance));
        bool initialBalanceChanged = InitialBalance != validatedInitialBalance;

        if (initialBalanceChanged && hasMovements)
        {
            throw new BusinessRuleException(
                "account_initial_balance_locked",
                "Initial balance cannot be changed after the first movement.");
        }

        if (Type == validatedType && !initialBalanceChanged && IsActive == isActive)
        {
            return false;
        }

        Type = validatedType;
        IsActive = isActive;
        if (initialBalanceChanged)
        {
            InitialBalance = validatedInitialBalance;
            CurrentBalance = validatedInitialBalance;
        }

        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
        return true;
    }

    public bool ChangeStatus(bool isActive, DateTimeOffset updatedAtUtc)
    {
        if (IsActive == isActive)
        {
            return false;
        }

        IsActive = isActive;
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
        return true;
    }

    public decimal ApplyMovement(MovementAmount amount, DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (!IsActive)
        {
            throw new BusinessRuleException(
                "account_inactive",
                "An inactive account cannot receive movements.");
        }

        decimal resultingBalance = CurrentBalance + amount.Value;
        if (resultingBalance < 0)
        {
            throw new InsufficientFundsException();
        }

        if (resultingBalance > MaximumBalance)
        {
            throw new BusinessRuleException(
                "account_balance_limit_exceeded",
                "The resulting balance exceeds the supported decimal(18,2) limit.");
        }

        CurrentBalance = resultingBalance;
        UpdatedAtUtc = occurredAtUtc.ToUniversalTime();
        return CurrentBalance;
    }

    public void RebuildBalance(decimal balance, DateTimeOffset rebuiltAtUtc)
    {
        CurrentBalance = ValidateBalance(balance, nameof(balance));
        UpdatedAtUtc = rebuiltAtUtc.ToUniversalTime();
    }

    private static string ValidateNumber(string number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new BusinessRuleException(
                "account_number_required",
                "Account number is required.");
        }

        string normalizedNumber = number.Trim();
        if (normalizedNumber.Length > MaximumNumberLength ||
            normalizedNumber.Any(character => !char.IsAsciiDigit(character)))
        {
            throw new BusinessRuleException(
                "account_number_invalid",
                $"Account number must contain only digits and cannot exceed {MaximumNumberLength} characters.");
        }

        return normalizedNumber;
    }

    private static AccountType ValidateType(AccountType type)
    {
        if (!Enum.IsDefined(type))
        {
            throw new BusinessRuleException("account_type_invalid", "The specified account type is invalid.");
        }

        return type;
    }

    private static decimal ValidateBalance(decimal balance, string fieldName)
    {
        if (balance < 0 ||
            balance > MaximumBalance ||
            decimal.Round(balance, 2, MidpointRounding.ToEven) != balance)
        {
            throw new BusinessRuleException(
                "account_balance_invalid",
                $"The {fieldName} must be non-negative, fit decimal(18,2), and have at most two decimals.");
        }

        return balance;
    }

    private static Guid ValidateCustomerId(Guid customerId)
    {
        if (customerId == Guid.Empty)
        {
            throw new BusinessRuleException("account_customer_invalid", "Customer identifier is invalid.");
        }

        return customerId;
    }
}
