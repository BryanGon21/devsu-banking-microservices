using Devsu.Customers.Domain.Entities;

namespace Devsu.Customers.Application.Ports;

public interface ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(
        Guid customerId,
        bool includeDeleted,
        CancellationToken cancellationToken);

    public Task<(IReadOnlyCollection<Customer> Items, int Total)> ListAsync(
        int skip,
        int take,
        bool? isActive,
        CancellationToken cancellationToken);

    public Task<bool> IdentificationExistsAsync(
        string identification,
        Guid? excludedCustomerId,
        CancellationToken cancellationToken);

    public void Add(Customer customer);
}
