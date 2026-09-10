using Devsu.Accounts.Domain.Enums;

namespace Devsu.Accounts.Application.Ports;

public interface IAccountStatementReader
{
    public Task<AccountStatementData?> GetAsync(
        Guid customerId,
        DateTimeOffset startInclusiveUtc,
        DateTimeOffset endExclusiveUtc,
        CancellationToken cancellationToken);
}

public sealed record AccountStatementData(
    Guid CustomerId,
    string CustomerName,
    IReadOnlyCollection<AccountStatementAccountData> Accounts);

public sealed record AccountStatementAccountData(
    string Number,
    AccountType Type,
    decimal InitialBalance,
    decimal OpeningBalance,
    decimal CurrentBalance,
    bool IsActive,
    IReadOnlyCollection<AccountStatementMovementData> Movements);

public sealed record AccountStatementMovementData(
    long Id,
    string AccountNumber,
    DateTimeOffset OccurredAtUtc,
    MovementType Type,
    decimal Value,
    decimal Balance);
