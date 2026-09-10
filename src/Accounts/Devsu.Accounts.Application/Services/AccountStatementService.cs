using Devsu.Accounts.Application.Contracts;
using Devsu.Accounts.Application.Exceptions;
using Devsu.Accounts.Application.Ports;

namespace Devsu.Accounts.Application.Services;

public sealed class AccountStatementService : IAccountStatementService
{
    private const int MaximumDateRangeDays = 366;

    private readonly IAccountStatementReader _statementReader;

    public AccountStatementService(IAccountStatementReader statementReader)
    {
        _statementReader = statementReader;
    }

    public async Task<AccountStatementResponse> GetAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        Guid? customerId,
        CancellationToken cancellationToken)
    {
        if (!startDate.HasValue || !endDate.HasValue || !customerId.HasValue)
        {
            throw new ValidationException(
                "report_parameters_required",
                "fechaInicio, fechaFin, and clienteId are required.");
        }

        if (customerId == Guid.Empty)
        {
            throw new ValidationException(
                "customer_id_invalid",
                "Customer identifier is invalid.");
        }

        ValidateDateRange(startDate.Value, endDate.Value);
        DateTimeOffset startInclusiveUtc = new(
            startDate.Value.ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero);
        DateTimeOffset endExclusiveUtc = new(
            endDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero);

        AccountStatementData statement = await _statementReader.GetAsync(
            customerId.Value,
            startInclusiveUtc,
            endExclusiveUtc,
            cancellationToken)
            ?? throw new NotFoundException(
                "customer_not_found",
                "The requested customer does not exist in the local projection.");

        AccountStatementAccountResponse[] accounts = statement.Accounts
            .OrderBy(account => account.Number, StringComparer.Ordinal)
            .Select(MapAccount)
            .ToArray();

        return new AccountStatementResponse(
            new AccountStatementCustomerResponse(statement.CustomerId, statement.CustomerName),
            startDate.Value,
            endDate.Value,
            accounts);
    }

    private static void ValidateDateRange(DateOnly startDate, DateOnly endDate)
    {
        if (startDate > endDate)
        {
            throw new ValidationException(
                "date_range_invalid",
                "Start date cannot be later than end date.");
        }

        if (endDate.DayNumber - startDate.DayNumber + 1 > MaximumDateRangeDays)
        {
            throw new ValidationException(
                "date_range_too_large",
                $"Date range cannot exceed {MaximumDateRangeDays} days.");
        }

        if (endDate == DateOnly.MaxValue)
        {
            throw new ValidationException(
                "end_date_out_of_range",
                "End date is outside the supported range.");
        }
    }

    private static AccountStatementAccountResponse MapAccount(
        AccountStatementAccountData account)
    {
        AccountStatementMovementData[] orderedMovementData = account.Movements
            .OrderBy(movement => movement.OccurredAtUtc)
            .ThenBy(movement => movement.Id)
            .ToArray();
        AccountStatementMovementResponse[] movements = orderedMovementData
            .Select(movement => new AccountStatementMovementResponse(
                movement.Id,
                movement.OccurredAtUtc,
                movement.Type,
                movement.Value,
                movement.Balance))
            .ToArray();
        decimal closingBalance = orderedMovementData.Length == 0
            ? account.OpeningBalance
            : orderedMovementData[^1].Balance;

        return new AccountStatementAccountResponse(
            account.Number,
            account.Type,
            account.InitialBalance,
            account.OpeningBalance,
            closingBalance,
            account.CurrentBalance,
            account.IsActive,
            movements);
    }
}
