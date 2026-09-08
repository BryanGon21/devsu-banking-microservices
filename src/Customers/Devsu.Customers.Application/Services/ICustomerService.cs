using Devsu.Customers.Application.Contracts;

namespace Devsu.Customers.Application.Services;

public interface ICustomerService
{
    public Task<CustomerResponse> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken);

    public Task<CustomerResponse> GetByIdAsync(Guid customerId, CancellationToken cancellationToken);

    public Task<PageResponse<CustomerResponse>> ListAsync(
        int page,
        int pageSize,
        bool? isActive,
        CancellationToken cancellationToken);

    public Task<CustomerResponse> UpdateAsync(
        Guid customerId,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken);

    public Task<CustomerResponse> ChangeStatusAsync(
        Guid customerId,
        ChangeCustomerStatusRequest request,
        CancellationToken cancellationToken);

    public Task DeleteAsync(Guid customerId, CancellationToken cancellationToken);
}
