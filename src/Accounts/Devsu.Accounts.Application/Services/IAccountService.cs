using Devsu.Accounts.Application.Contracts;

namespace Devsu.Accounts.Application.Services;

public interface IAccountService
{
    public Task<AccountResponse> CreateAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken);

    public Task<AccountResponse> GetByNumberAsync(
        string number,
        CancellationToken cancellationToken);

    public Task<PageResponse<AccountResponse>> ListAsync(
        int page,
        int pageSize,
        Guid? customerId,
        bool? isActive,
        CancellationToken cancellationToken);

    public Task<AccountResponse> UpdateAsync(
        string number,
        UpdateAccountRequest request,
        CancellationToken cancellationToken);

    public Task<AccountResponse> ChangeStatusAsync(
        string number,
        ChangeAccountStatusRequest request,
        CancellationToken cancellationToken);
}
