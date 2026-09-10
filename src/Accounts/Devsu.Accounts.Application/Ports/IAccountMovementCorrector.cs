using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Domain.ValueObjects;

namespace Devsu.Accounts.Application.Ports;

public interface IAccountMovementCorrector
{
    public Task<AccountMovementCorrectionResult> CorrectAsync(
        long movementId,
        MovementAmount amount,
        DateTimeOffset occurredAtUtc,
        string reason,
        string correlationId,
        DateTimeOffset correctedAtUtc,
        CancellationToken cancellationToken);
}

public sealed record AccountMovementCorrectionResult(
    AccountMovement Movement,
    bool WasCorrected);
