using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Domain.Enums;
using Devsu.Accounts.Domain.Exceptions;

namespace Devsu.Accounts.UnitTests.Domain;

public sealed class AccountTests
{
    private static readonly DateTimeOffset CreationTime =
        new(2026, 2, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidData_PreservesNumberAndInitializesCurrentBalance()
    {
        Guid customerId = Guid.NewGuid();

        Account account = Account.Create(
            "001234",
            AccountType.Savings,
            2_000.50m,
            true,
            customerId,
            CreationTime);

        Assert.Equal("001234", account.Number);
        Assert.Equal(AccountType.Savings, account.Type);
        Assert.Equal(2_000.50m, account.InitialBalance);
        Assert.Equal(2_000.50m, account.CurrentBalance);
        Assert.True(account.IsActive);
        Assert.Equal(customerId, account.CustomerId);
        Assert.Equal(CreationTime, account.CreatedAtUtc);
        Assert.Equal(CreationTime, account.UpdatedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123-456")]
    [InlineData("ABC123")]
    public void Create_WithInvalidNumber_ThrowsBusinessRule(string number)
    {
        BusinessRuleException exception = Assert.Throws<BusinessRuleException>(() =>
            CreateAccount(number: number));

        Assert.StartsWith("account_number_", exception.Code, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("10.001")]
    public void Create_WithInvalidInitialBalance_ThrowsBusinessRule(string balance)
    {
        decimal value = decimal.Parse(balance, System.Globalization.CultureInfo.InvariantCulture);

        BusinessRuleException exception = Assert.Throws<BusinessRuleException>(() =>
            CreateAccount(initialBalance: value));

        Assert.Equal("account_balance_invalid", exception.Code);
    }

    [Fact]
    public void Update_InitialBalanceBeforeMovements_UpdatesBothBalances()
    {
        Account account = CreateAccount();
        DateTimeOffset updateTime = CreationTime.AddHours(1);

        bool changed = account.Update(
            AccountType.Checking,
            500m,
            false,
            hasMovements: false,
            updateTime);

        Assert.True(changed);
        Assert.Equal(AccountType.Checking, account.Type);
        Assert.Equal(500m, account.InitialBalance);
        Assert.Equal(500m, account.CurrentBalance);
        Assert.False(account.IsActive);
        Assert.Equal(updateTime, account.UpdatedAtUtc);
    }

    [Fact]
    public void Update_InitialBalanceAfterMovement_ThrowsBusinessRule()
    {
        Account account = CreateAccount();

        BusinessRuleException exception = Assert.Throws<BusinessRuleException>(() =>
            account.Update(
                AccountType.Savings,
                500m,
                true,
                hasMovements: true,
                CreationTime.AddHours(1)));

        Assert.Equal("account_initial_balance_locked", exception.Code);
        Assert.Equal(100m, account.InitialBalance);
        Assert.Equal(100m, account.CurrentBalance);
    }

    [Fact]
    public void ChangeStatus_WithSameValue_IsIdempotent()
    {
        Account account = CreateAccount();

        bool changed = account.ChangeStatus(true, CreationTime.AddHours(1));

        Assert.False(changed);
        Assert.Equal(CreationTime, account.UpdatedAtUtc);
    }

    private static Account CreateAccount(
        string number = "123456",
        decimal initialBalance = 100m)
    {
        return Account.Create(
            number,
            AccountType.Savings,
            initialBalance,
            true,
            Guid.NewGuid(),
            CreationTime);
    }
}
