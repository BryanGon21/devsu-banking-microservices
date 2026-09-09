using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Domain.ValueObjects;

namespace Devsu.Accounts.Application.Ports;

public interface IAccountMovementWriter
{
    public Task<AccountMovementWriteResult> CreateAsync(
        string accountNumber,
        MovementAmount amount,
        string idempotencyKey,
        string requestFingerprint,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken);
}

public sealed record AccountMovementWriteResult(
    AccountMovement Movement,
    bool WasCreated);
