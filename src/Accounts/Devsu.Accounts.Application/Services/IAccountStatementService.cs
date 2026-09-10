using Devsu.Accounts.Application.Contracts;

namespace Devsu.Accounts.Application.Services;

public interface IAccountStatementService
{
    public Task<AccountStatementResponse> GetAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        Guid? customerId,
        CancellationToken cancellationToken);
}
