using Devsu.Accounts.Domain.Enums;
using Devsu.Accounts.Domain.Exceptions;
using Devsu.Accounts.Domain.ValueObjects;

namespace Devsu.Accounts.UnitTests.Domain;

public sealed class MovementAmountTests
{
    [Theory]
    [InlineData(MovementType.Deposit, "100.00")]
    [InlineData(MovementType.Withdrawal, "-100.00")]
    public void Create_WithMatchingTypeAndSign_Succeeds(MovementType type, string value)
    {
        decimal parsedValue = decimal.Parse(
            value,
            System.Globalization.CultureInfo.InvariantCulture);

        MovementAmount amount = MovementAmount.Create(type, parsedValue);

        Assert.Equal(type, amount.Type);
        Assert.Equal(parsedValue, amount.Value);
    }

    [Theory]
    [InlineData(MovementType.Deposit, "-1.00")]
    [InlineData(MovementType.Withdrawal, "1.00")]
    public void Create_WithMismatchedSign_IsRejected(MovementType type, string value)
    {
        decimal parsedValue = decimal.Parse(
            value,
            System.Globalization.CultureInfo.InvariantCulture);

        BusinessRuleException exception = Assert.Throws<BusinessRuleException>(() =>
            MovementAmount.Create(type, parsedValue));

        Assert.Equal("movement_value_sign_invalid", exception.Code);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("10.001")]
    public void Create_WithInvalidPrecisionOrZero_IsRejected(string value)
    {
        decimal parsedValue = decimal.Parse(
            value,
            System.Globalization.CultureInfo.InvariantCulture);

        BusinessRuleException exception = Assert.Throws<BusinessRuleException>(() =>
            MovementAmount.Create(MovementType.Deposit, parsedValue));

        Assert.Equal("movement_value_invalid", exception.Code);
    }
}
