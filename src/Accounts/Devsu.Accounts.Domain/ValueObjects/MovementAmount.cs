using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Domain.Enums;
using Devsu.Accounts.Domain.Exceptions;

namespace Devsu.Accounts.Domain.ValueObjects;

public sealed record MovementAmount
{
    private MovementAmount(MovementType type, decimal value)
    {
        Type = type;
        Value = value;
    }

    public MovementType Type { get; }

    public decimal Value { get; }

    public static MovementAmount Create(MovementType type, decimal value)
    {
        if (!Enum.IsDefined(type))
        {
            throw new BusinessRuleException(
                "movement_type_invalid",
                "The specified movement type is invalid.");
        }

        if (value == 0 ||
            decimal.Abs(value) > Account.MaximumBalance ||
            decimal.Round(value, 2, MidpointRounding.ToEven) != value)
        {
            throw new BusinessRuleException(
                "movement_value_invalid",
                "Movement value must be non-zero, fit decimal(18,2), and have at most two decimals.");
        }

        if (type == MovementType.Deposit && value < 0)
        {
            throw new BusinessRuleException(
                "movement_value_sign_invalid",
                "A deposit requires a positive value.");
        }

        if (type == MovementType.Withdrawal && value > 0)
        {
            throw new BusinessRuleException(
                "movement_value_sign_invalid",
                "A withdrawal requires a negative value.");
        }

        return new MovementAmount(type, value);
    }
}
