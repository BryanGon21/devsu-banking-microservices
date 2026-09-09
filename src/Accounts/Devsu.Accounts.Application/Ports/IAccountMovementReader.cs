using Devsu.Accounts.Domain.Entities;

namespace Devsu.Accounts.Application.Ports;

public interface IAccountMovementReader
{
    public Task<AccountMovement?> GetByIdAsync(
        long movementId,
        CancellationToken cancellationToken);

    public Task<(IReadOnlyCollection<AccountMovement> Items, int TotalItems)> ListAsync(
        int skip,
        int take,
        string? accountNumber,
        DateTimeOffset? startInclusiveUtc,
        DateTimeOffset? endExclusiveUtc,
        CancellationToken cancellationToken);
}
