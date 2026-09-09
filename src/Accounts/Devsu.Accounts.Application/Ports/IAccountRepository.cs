using Devsu.Accounts.Domain.Entities;

namespace Devsu.Accounts.Application.Ports;

public interface IAccountRepository
{
    public Task AddAsync(Account account, CancellationToken cancellationToken);

    public Task<bool> ExistsAsync(string number, CancellationToken cancellationToken);

    public Task<Account?> GetByNumberAsync(string number, CancellationToken cancellationToken);

    public Task<bool> HasMovementsAsync(string number, CancellationToken cancellationToken);

    public Task<(IReadOnlyCollection<Account> Items, int TotalItems)> ListAsync(
        int skip,
        int take,
        Guid? customerId,
        bool? isActive,
        CancellationToken cancellationToken);
}
